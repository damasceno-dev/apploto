'use client';

import { useEffect, useState } from 'react';
import { useDeviceDraft } from '@/client/drafts/use-device-draft';
import { beforeGateEvent } from '@/client/session/session-provider';

/** Retain candidates, never confirmations or server versions; restoration is always deliberate. */
export function useGoLiveDraft<T>(context: string, value: T, authorityVerified: boolean) {
  const draft = useDeviceDraft<T>(context, { authorityVerified });
  const [dirty, setDirty] = useState(false);
  const [storageFailed, setStorageFailed] = useState(false);
  const encoded = JSON.stringify(value);
  const { save, clear } = draft;
  useEffect(() => {
    if (!dirty) return;
    const retain = () => setStorageFailed(!save(JSON.parse(encoded) as T).ok);
    retain();
    window.addEventListener(beforeGateEvent, retain);
    return () => window.removeEventListener(beforeGateEvent, retain);
  }, [dirty, encoded, save]);
  return {
    ...draft,
    storageFailed,
    markDirty: () => setDirty(true),
    clear: () => {
      setDirty(false);
      clear();
    },
  };
}
