import { Card } from '@/components/ui/card';
import { PasswordLogin } from '@/components/entry/password-login';
import { ThemeControl } from '@/theme/theme-control';

export default function LoginPage() {
  return (
    <>
      <header className="flex flex-wrap items-center justify-between gap-stack-md bg-nav-bg p-4 text-nav-text-strong">
        <span className="type-section-title">Lotero</span>
        <ThemeControl iconsOnly />
      </header>
      <main className="mx-auto flex max-w-dialog-max flex-col gap-stack-lg px-page-inline-compact py-page-block-start">
        <Card variant="sheet" eyebrow="Lotero">
          <h1 className="type-page-title">Entrar</h1>
          <PasswordLogin />
        </Card>
      </main>
    </>
  );
}
