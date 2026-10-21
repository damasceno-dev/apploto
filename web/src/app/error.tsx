'use client';

export default function ErrorBoundary({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <main>
      <h1>Não foi possível carregar a página</h1>
      <button onClick={reset}>Tentar novamente</button>
    </main>
  );
}
