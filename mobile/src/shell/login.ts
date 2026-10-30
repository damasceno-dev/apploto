import type { SessionEngine } from '../session/engine';
import { actionFailure, type ActionFailure } from '../data/failures';
interface LoginState {
  readonly email: string;
  readonly password: string;
  readonly pending: boolean;
  readonly failure: ActionFailure | null;
}
/** In-memory form controller survives a session/query scope replacement; no persistence or second auth path. */
export class LoginForm {
  private state: LoginState = { email: '', password: '', pending: false, failure: null };
  private listeners = new Set<() => void>();
  constructor(private readonly engine: SessionEngine) {
    let previousUser = engine.getSnapshot().user?.userId ?? null;
    engine.subscribe(() => {
      const user = engine.getSnapshot().user?.userId ?? null;
      if (previousUser && !user) this.publish({ email: '', password: '', failure: null });
      previousUser = user;
    });
  }
  get = () => this.state;
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  private publish(update: Partial<LoginState>) {
    this.state = { ...this.state, ...update };
    this.listeners.forEach((listener) => listener());
  }
  email = (email: string) => {
    if (!this.state.pending) this.publish({ email });
  };
  password = (password: string) => {
    if (!this.state.pending) this.publish({ password });
  };
  clearPassword = () => {
    if (this.state.password) this.publish({ password: '' });
  };
  async submit() {
    if (this.state.pending || !this.state.email.trim() || !this.state.password) return;
    const request = { email: this.state.email.trim(), password: this.state.password };
    this.publish({ pending: true, password: '', failure: null });
    try {
      await this.engine.login(request);
    } catch (error) {
      this.publish({ failure: actionFailure(error) });
    } finally {
      request.password = '';
      this.publish({ pending: false });
    }
  }
}
const forms = new WeakMap<SessionEngine, LoginForm>();
export function loginForm(engine: SessionEngine) {
  let form = forms.get(engine);
  if (!form) {
    form = new LoginForm(engine);
    forms.set(engine, form);
  }
  return form;
}
