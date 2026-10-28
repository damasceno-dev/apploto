/** UIAutomator chooses either XML quote style, notably single quotes around JSON input. */
export function nativeNodes(xml) {
  const decode = (value) =>
    value
      .replace(/&quot;/g, '"')
      .replace(/&apos;/g, "'")
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>')
      .replace(/&#(\d+);/g, (_, point) => String.fromCodePoint(Number(point)))
      .replace(/&amp;/g, '&');
  return [...xml.matchAll(/<node\b[^>]*>/g)].map((match) =>
    Object.fromEntries(
      [...match[0].matchAll(/([\w-]+)=(["'])([\s\S]*?)\2/g)].map(([, key, , value]) => [
        key,
        decode(value),
      ]),
    ),
  );
}
