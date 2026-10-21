'use client';

export default function GlobalError({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="pt-BR">
      <body>
        <h1>Não foi possível carregar o aplicativo</h1>
        <button onClick={reset}>Tentar novamente</button>
      </body>
    </html>
  );
}
