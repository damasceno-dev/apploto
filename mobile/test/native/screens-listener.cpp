// Compiles the installed, patched listener, with a minimal renderer data stub. This verifies
// concurrent callback lifetime/ownership; actual Fabric/JSI integration is tested on Android.
#include "RNSScreenRemovalListener.h"
#include <atomic>
#include <cassert>
#include <thread>
#include <vector>

int main() {
  RNSScreenRemovalListener listener;
  auto transaction = [&](int tag) {
    ShadowViewMutation mutation;
    mutation.type = ShadowViewMutation::Type::Remove;
    mutation.oldChildShadowView.componentName = "RNSScreen";
    mutation.oldChildShadowView.tag = tag;
    return listener.pullTransaction(7, 12, {}, {mutation});
  };
  std::atomic<int> oldCalls{0}, newCalls{0};
  auto oldToken = listener.setListener([&](int tag) { assert(tag == 42); ++oldCalls; });
  transaction(42);
  assert(oldCalls == 1);
  auto newToken = listener.setListener([&](int tag) { assert(tag == 42); ++newCalls; });
  listener.clearListener(oldToken); // Late teardown must not disarm the replacement host.
  transaction(42);
  assert(newCalls == 1 && oldCalls == 1);
  listener.clearListener(newToken);
  auto disarmed = transaction(42);
  assert(newCalls == 1);
  assert(disarmed && disarmed->surfaceId == 7 && disarmed->number == 12 && disarmed->mutations.size() == 1);
  // Callback may reenter installation; invoking it while holding the listener lock deadlocks.
  listener.setListener([&](int) { listener.setListener([&](int) { ++newCalls; }); });
  transaction(42);
  transaction(42);
  assert(newCalls == 2);
  std::vector<std::thread> threads;
  for (int worker = 0; worker < 8; ++worker) {
    threads.emplace_back([&]() {
      for (int i = 0; i < 10000; ++i) {
        auto token = listener.setListener([&](int) { ++newCalls; });
        transaction(42);
        listener.clearListener(token);
      }
    });
  }
  for (auto &thread : threads) thread.join();
  assert(newCalls > 2);
}
