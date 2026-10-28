import {
  createContext,
  useContext,
  useEffect,
  useLayoutEffect,
  useMemo,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react';
import { AppState, Text, View } from 'react-native';
import NetInfo from '@react-native-community/netinfo';
import { SafeAreaView } from 'react-native-safe-area-context';
import {
  QueryClient,
  QueryClientProvider,
  focusManager,
  onlineManager,
} from '@tanstack/react-query';
import { configureApiClient, clearApiClientConfiguration, type ApiClient } from '@loto/api';
import { SessionEngine } from './engine';
import { readApiConfiguration } from '../config/api';
import {
  nativeCredentials,
  nativeDeviceStore,
  clearNativeSelections,
  emailPartitionKey,
  secureUuid,
} from '../storage/native';
import { ScopedDeviceStore, DeviceDraftStore, type RetainedDataReview } from '../storage/scoped';
import { NavigationIntentStore } from '../data/navigation';

// Keep the fault-injection module out of Metro's production dependency graph.
const appTransport: typeof fetch = __DEV__
  ? // eslint-disable-next-line @typescript-eslint/no-require-imports
    require('../probes/session-transport').sessionTransport
  : (input, init) => globalThis.fetch(input, init);

interface SessionServices {
  readonly engine: SessionEngine;
  readonly store: ScopedDeviceStore;
  readonly drafts: DeviceDraftStore;
  readonly navigation: NavigationIntentStore;
  readonly discardRetainedData: (
    review: RetainedDataReview,
    confirmLoss: boolean,
  ) => Promise<boolean>;
}
const SessionContext = createContext<SessionServices | null>(null);
const ApiContext = createContext<ApiClient | null>(null);
let defaultOwner: ApiClient | null = null;
export function useOptionalSession() {
  return useContext(SessionContext);
}
export function useSession() {
  const value = useOptionalSession();
  if (!value) throw new Error('SessionProvider is not configured.');
  return {
    ...value,
    snapshot: useSyncExternalStore(
      value.engine.subscribe,
      value.engine.getSnapshot,
      value.engine.getSnapshot,
    ),
  };
}
export function useScopedApiClient() {
  const client = useContext(ApiContext);
  if (!client) throw new Error('Scoped API provider is not configured.');
  return client;
}

export function SessionProvider({
  engine,
  children,
  lifecycle = true,
}: {
  engine: SessionEngine;
  children: ReactNode;
  lifecycle?: boolean;
}) {
  const snapshot = useSyncExternalStore(engine.subscribe, engine.getSnapshot, engine.getSnapshot);
  const services = useMemo(() => {
    const store = new ScopedDeviceStore(nativeDeviceStore, engine, emailPartitionKey);
    const navigation = new NavigationIntentStore(nativeDeviceStore, engine);
    return {
      engine,
      store,
      drafts: new DeviceDraftStore(store, secureUuid),
      navigation,
      discardRetainedData: async (review: RetainedDataReview, confirmLoss: boolean) => {
        const discarded = await store.discardRetainedData(review, confirmLoss);
        if (discarded) await navigation.acknowledge();
        return discarded;
      },
    };
  }, [engine]);
  useEffect(() => {
    void engine.start().catch(() => undefined);
  }, [engine]);
  useEffect(() => {
    if (!lifecycle) return;
    const active = AppState.addEventListener('change', (state) => {
      const focused = state === 'active';
      focusManager.setFocused(focused);
      void engine.foreground(focused).catch(() => undefined);
    });
    const unsubscribe = NetInfo.addEventListener((state) => {
      const online = state.isConnected === true && state.isInternetReachable !== false;
      onlineManager.setOnline(online);
      void engine.connectivity(online).catch(() => undefined);
    });
    const timer = setInterval(() => {
      if (AppState.currentState === 'active') void engine.refresh().catch(() => undefined);
    }, 60000);
    return () => {
      active.remove();
      unsubscribe();
      clearInterval(timer);
    };
  }, [engine, lifecycle]);
  return (
    <SessionContext.Provider value={services}>
      <ScopeBoundary key={snapshot.scope} engine={engine}>
        {children}
      </ScopeBoundary>
    </SessionContext.Provider>
  );
}
function ScopeBoundary({ engine, children }: { engine: SessionEngine; children: ReactNode }) {
  const [query] = useState(
    () =>
      new QueryClient({
        defaultOptions: { queries: { retry: false, staleTime: 30000 }, mutations: { retry: 0 } },
      }),
  );
  const [client] = useState(() => engine.client());
  // Register only after commit, before query passive effects; abandoned renders have no global side effects.
  useLayoutEffect(() => {
    configureApiClient(client);
    defaultOwner = client;
    return () => {
      if (defaultOwner === client) {
        defaultOwner = null;
        clearApiClientConfiguration();
      }
    };
  }, [client]);
  useEffect(
    () => () => {
      void query
        .cancelQueries()
        .catch(() => undefined)
        .then(() => query.clear());
    },
    [query, client],
  );
  return (
    <ApiContext.Provider value={client}>
      <QueryClientProvider client={query}>{children}</QueryClientProvider>
    </ApiContext.Provider>
  );
}
export function AppSessionProvider({ children }: { children: ReactNode }) {
  const [runtime] = useState(() => {
    try {
      return {
        engine: new SessionEngine({
          baseUrl: readApiConfiguration(),
          credentials: nativeCredentials,
          fetch: appTransport,
          monotonicNow: () => performance.now(),
          clearSelections: clearNativeSelections,
        }),
        error: null,
      };
    } catch (error) {
      return {
        engine: null,
        error: error instanceof Error ? error.message : 'A API do aplicativo não está configurada.',
      };
    }
  });
  if (!runtime.engine)
    return (
      <View className="flex-1">
        <SafeAreaView edges={['top', 'left', 'right']} className="px-4">
          <Text accessibilityRole="alert" className="text-helper text-sem-feedback-danger-fg">
            {runtime.error}
          </Text>
        </SafeAreaView>
        {__DEV__ ? children : null}
      </View>
    );
  return <SessionProvider engine={runtime.engine}>{children}</SessionProvider>;
}
