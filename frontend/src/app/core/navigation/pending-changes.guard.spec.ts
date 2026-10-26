import { pendingChangesGuard, PendingChangesAware } from './pending-changes.guard';

describe('pendingChangesGuard', () => {
  const runGuard = (component: PendingChangesAware) =>
    pendingChangesGuard(component, {} as never, {} as never, {} as never);

  it('allows navigation when the component has no pending changes', () => {
    const component: PendingChangesAware = {
      canDeactivate: () => true,
    };

    expect(runGuard(component)).toBeTrue();
  });

  it('returns the component confirmation result for pending changes', async () => {
    const component: PendingChangesAware = {
      canDeactivate: () => Promise.resolve(false),
    };

    await expectAsync(runGuard(component) as Promise<boolean>).toBeResolvedTo(
      false,
    );
  });
});
