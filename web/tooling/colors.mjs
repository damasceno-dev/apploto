import names from 'color-name';
import valueParser from 'postcss-value-parser';

// CSS parser keeps quoted text, fragments and URLs out of color detection.
export function hasRawColor(value, includeNames = true) {
  // A complete URL is configuration/content, even if its fragment resembles hex.
  if (/^(?:[a-z][a-z0-9+.-]*:\/\/|\/|\.{1,2}\/)[^\s]+$/i.test(value.trim())) return false;
  let found = false;
  valueParser(value).walk((node) => {
    if (node.type === 'function') {
      if (/^url$/i.test(node.value)) return false;
      if (/^(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color|color-mix)$/i.test(node.value))
        found = true;
    }
    if (
      node.type === 'word' &&
      (/^#[\da-f]{3,8}$/i.test(node.value) ||
        (includeNames &&
          (Object.hasOwn(names, node.value.toLowerCase()) ||
            node.value.toLowerCase() === 'transparent')))
    )
      found = true;
  });
  return found;
}

// Outside styles, a reference embedded in copy is not a palette literal.
function isExplicitColor(value) {
  const nodes = valueParser(value.trim()).nodes;
  if (nodes.length !== 1) return false;
  const [node] = nodes;
  return (
    (node.type === 'word' && /^#[\da-f]{3,8}$/i.test(node.value)) ||
    (node.type === 'function' &&
      !node.unclosed &&
      /^(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color|color-mix)$/i.test(node.value))
  );
}

export function hasArbitraryColor(value) {
  return [...value.matchAll(/\[([^\]]+)\]/g)].some((match) =>
    hasRawColor(match[1].replaceAll('_', ' ')),
  );
}

function isColorProperty(key) {
  const value = String(key);
  if (value.startsWith('--')) return true;
  return /^(?:colors?|background(?:color|image)?|border(?:top|right|bottom|left|start|end|block|inline)?(?:color)?|outline(?:color)?|boxshadow|textshadow|shadowcolor|tintcolor|fill|stroke|caretcolor|accentcolor|textdecoration(?:color)?|filter)$/i.test(
    value.replaceAll('-', ''),
  );
}

export const rawColorRule = {
  meta: {
    type: 'problem',
    schema: [],
    messages: { raw: 'Use semantic design tokens instead of raw colors.' },
  },
  create(context) {
    const reported = [];
    function report(node) {
      // Parent visitors run first; do not report the same literal again in child visitors.
      const [start, end] = node.range;
      if (reported.some(([from, to]) => start >= from && end <= to)) return;
      reported.push([start, end]);
      context.report({ node, messageId: 'raw' });
    }
    function isUrlFragment(node, value) {
      if (!value.startsWith('#')) return false;
      const parent = node.parent;
      return (
        (parent?.type === 'JSXAttribute' && ['href', 'src'].includes(parent.name.name)) ||
        (parent?.type === 'Property' &&
          ['href', 'src', 'uri'].includes(parent.key.name ?? parent.key.value))
      );
    }
    function inspect(node, value) {
      if (typeof value !== 'string') return;
      let inStyle = false;
      for (let parent = node.parent; parent; parent = parent.parent) {
        if (parent.type === 'Property') {
          const key = parent.key.name ?? parent.key.value;
          if (isColorProperty(key)) inStyle = true;
        }
        if (
          parent.type === 'JSXAttribute' &&
          ['style', 'color', 'fill', 'stroke'].includes(parent.name.name)
        )
          inStyle = true;
      }
      if (
        hasArbitraryColor(value) ||
        (!isUrlFragment(node, value) && isExplicitColor(value)) ||
        (inStyle && hasRawColor(value))
      )
        report(node);
    }
    function strings(node, seen = new Set()) {
      if (!node || seen.has(node)) return [];
      seen.add(node);
      if (node.type === 'Literal') return typeof node.value === 'string' ? [node.value] : [];
      if (node.type === 'Identifier') {
        let scope = context.sourceCode.getScope(node);
        while (scope) {
          const variable = scope.set.get(node.name);
          if (variable)
            return variable.defs.flatMap((definition) => strings(definition.node.init, seen));
          scope = scope.upper;
        }
      }
      if (node.type === 'TemplateLiteral') {
        const pieces = node.quasis.map((part) => part.value.cooked ?? '');
        return [
          pieces
            .map((piece, index) => piece + (strings(node.expressions[index], seen)[0] ?? ''))
            .join(''),
        ];
      }
      if (node.type === 'BinaryExpression' && node.operator === '+')
        return [strings(node.left, seen).join('') + strings(node.right, seen).join('')];
      if (node.type === 'ConditionalExpression')
        return [...strings(node.consequent, seen), ...strings(node.alternate, seen)];
      return [];
    }
    return {
      Property(node) {
        const key = node.key.name ?? node.key.value;
        if (isColorProperty(key) && strings(node.value).some((value) => hasRawColor(value)))
          report(node.value);
      },
      JSXAttribute(node) {
        const value =
          node.value?.type === 'JSXExpressionContainer' ? node.value.expression : node.value;
        const values = strings(value);
        const color = ['color', 'fill', 'stroke'].includes(node.name.name);
        if (values.some((value) => hasArbitraryColor(value) || (color && hasRawColor(value))))
          report(value);
      },
      Literal(node) {
        inspect(node, node.value);
      },
      TemplateElement(node) {
        inspect(node, node.value.cooked);
      },
    };
  },
};
