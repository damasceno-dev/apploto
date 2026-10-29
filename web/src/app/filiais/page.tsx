import { requireIdentity } from '@/server/gate/guards';
import { BranchPicker } from '@/components/entry/branch-picker';
export default async function BranchPickerPage() {
  await requireIdentity();
  return <BranchPicker />;
}
