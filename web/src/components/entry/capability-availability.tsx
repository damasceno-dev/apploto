'use client';
import { useEffect } from 'react';
import { FeatureAvailability } from './feature-availability';
export function CapabilityAvailability({ title }: { readonly title: string }) {
  useEffect(() => {
    if (window.location.hash)
      window.history.replaceState(window.history.state, '', window.location.pathname);
  }, []);
  return <FeatureAvailability title={title} back="/entrar" />;
}
