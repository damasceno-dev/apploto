/** A `Storage` in memory that can be made to fail, for device-storage tests. */
export class MemoryStorage implements Storage {
  private readonly items = new Map<string, string>();
  failWrites = false;
  failReads = false;

  get length() {
    return this.items.size;
  }
  clear() {
    this.items.clear();
  }
  getItem(key: string) {
    if (this.failReads) throw new DOMException('denied', 'SecurityError');
    return this.items.get(key) ?? null;
  }
  key(index: number) {
    return [...this.items.keys()][index] ?? null;
  }
  removeItem(key: string) {
    this.items.delete(key);
  }
  setItem(key: string, value: string) {
    if (this.failWrites) throw new DOMException('full', 'QuotaExceededError');
    this.items.set(key, value);
  }
  keys() {
    return [...this.items.keys()];
  }
}
