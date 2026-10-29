'use client';

import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Button, ButtonLink } from '@/components/ui/button';
import { FormField } from '@/components/ui/form-field';
import { Input, PasswordInput } from '@/components/ui/input';
import { ErrorSummary, Notice } from '@/components/ui/notice';
import { useSession } from '@/client/session/session-provider';
import { landingFor } from '@/lib/route-policy';
import { sessionCopy } from '@/lib/session-copy';

export function PasswordLogin() {
  const session = useSession();
  const busy = useRef(false);
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  const [pending, setPending] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [messages, setMessages] = useState<readonly string[] | null>(null);
  const [unconfirmed, setUnconfirmed] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [rateLimited, setRateLimited] = useState(false);

  const [logoutNotice, setLogoutNotice] = useState(false);
  useEffect(() => {
    try {
      const notice = sessionStorage.getItem('loto-logout-notice');
      sessionStorage.removeItem('loto-logout-notice');
      if (notice === 'unconfirmed') requestAnimationFrame(() => setLogoutNotice(true));
    } catch {
      /* Optional explanation only. */
    }
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy.current) return;
    busy.current = true;
    setPending(true);
    setMessages(null);
    setUnconfirmed(false);
    setRateLimited(false);
    try {
      const result = await session.login(email.trim(), password);
      if (!alive.current) return;
      if (result.ok) {
        setPassword('');
        window.location.assign(landingFor(result.snapshot));
        return;
      }
      setMessages(
        result.errorMessages.length > 0 ? result.errorMessages : [sessionCopy.noUsableErrorBody],
      );
      if (result.status >= 500) {
        setUnconfirmed(true);
        setMessages([sessionCopy.confirmationWarning, ...result.errorMessages]);
      }
      setRateLimited(result.status === 429);
    } catch {
      setUnconfirmed(true);
      setMessages([sessionCopy.confirmationWarning]);
    } finally {
      setPassword('');
      setAttempt((value) => value + 1);
      busy.current = false;
      setPending(false);
    }
  }

  return (
    <form
      onSubmit={(event) => void submit(event)}
      className="flex flex-col gap-stack-md"
      aria-busy={pending}
    >
      {logoutNotice ? <Notice kind="info" title={sessionCopy.logoutUnconfirmed} /> : null}
      {messages === null &&
      (session.snapshot.state === 'renewal-unconfirmed' || session.recovering) ? (
        <Notice
          kind="warning"
          title={sessionCopy.resultNotConfirmed}
          actions={
            <Button variant="secondary" onClick={() => void session.refresh()}>
              Verificar sessão
            </Button>
          }
        >
          {sessionCopy.renewalFailed}
        </Notice>
      ) : null}
      {messages !== null ? (
        <ErrorSummary
          key={attempt}
          title={
            rateLimited
              ? null
              : unconfirmed
                ? sessionCopy.resultNotConfirmed
                : 'Não foi possível entrar'
          }
          items={messages.map((message) => ({ message }))}
        />
      ) : null}
      <FormField label="E-mail" required>
        <Input
          type="email"
          autoComplete="username"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          readOnly={pending}
        />
      </FormField>
      <FormField label="Senha" required>
        <PasswordInput
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          readOnly={pending}
        />
      </FormField>
      <Button type="submit" loading={pending} loadingLabel="Entrando…">
        Entrar
      </Button>
      <ButtonLink href="/entrar/esqueci-senha">Esqueci minha senha</ButtonLink>
      <Notice kind="info" title="Recuperação de acesso e Google">
        Essas opções estarão disponíveis na etapa de conta e acesso. Primeiro acesso? Abra o link do
        convite recebido por e-mail.
      </Notice>
    </form>
  );
}
