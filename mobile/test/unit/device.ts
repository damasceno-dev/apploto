import type { DeviceStore } from '../../src/storage/ports';
export class MemoryDevice implements DeviceStore {
  readonly values = new Map<string, string>();
  fail = false;
  async getItem(key: string) {
    if (this.fail) throw new Error('device unavailable');
    return this.values.get(key) ?? null;
  }
  async setItem(key: string, value: string) {
    if (this.fail) throw new Error('device unavailable');
    this.values.set(key, value);
  }
  async removeItem(key: string) {
    if (this.fail) throw new Error('device unavailable');
    this.values.delete(key);
  }
  async getAllKeys() {
    return [...this.values.keys()];
  }
}
