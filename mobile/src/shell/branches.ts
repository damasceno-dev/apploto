import type { ResponseBranchSummaryJson } from '@loto/api';
import type { SessionEngine } from '../session/engine';
import { actionFailure, type ActionFailure } from '../data/failures';
interface ChoiceState {
  readonly branches: readonly ResponseBranchSummaryJson[] | null;
  readonly pending: string | null;
  readonly failure: ActionFailure | null;
}
/** Presentation only, bound to one verified identity revision; the existing engine owns every command. */
class BranchForm {
  private state: ChoiceState = { branches: null, pending: null, failure: null };
  private listeners = new Set<() => void>();
  readonly attempted = new Set<string>();
  get = () => this.state;
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  private publish(update: Partial<ChoiceState>) {
    this.state = { ...this.state, ...update };
    this.listeners.forEach((listener) => listener());
  }
  list(branches: readonly ResponseBranchSummaryJson[]) {
    if (this.state.branches !== branches) this.publish({ branches });
  }
  async choose(id: string, action: () => Promise<void>) {
    if (this.state.pending !== null) return;
    this.publish({ pending: id, failure: null });
    try {
      await action();
    } catch (error) {
      this.publish({ failure: actionFailure(error) });
    } finally {
      this.publish({ pending: null });
    }
  }
}
const choices = new WeakMap<SessionEngine, { revision: number; form: BranchForm }>();
export function branchForm(engine: SessionEngine) {
  const saved = choices.get(engine),
    revision = engine.identityRevision();
  if (saved?.revision === revision) return saved.form;
  const form = new BranchForm();
  choices.set(engine, { revision, form });
  return form;
}
