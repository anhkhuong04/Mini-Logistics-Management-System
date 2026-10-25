import { screen, waitFor } from '@testing-library/react';
import { expect, it } from 'vitest';
import { renderWithProviders } from '../../test/renderWithProviders';
import { useSettingsCommand } from './useSettingsCommand';
it('does not let an older authoritative refresh unlock a later unknown command outcome', async () => {
  let finish!: () => void; const read = new Promise<void>(resolve => { finish = resolve; }); let writes = 0;
  function Probe() {
    const command = useSettingsCommand();
    return <><button onClick={() => { const generation = command.snapshot(); void read.then(() => command.verified(generation)); }}>Refresh</button>
      <button disabled={command.blocked || command.pending} onClick={() => { void command.run(async () => { writes++; throw new TypeError('Unknown'); }); }}>Write</button></>;
  }
  const { user } = renderWithProviders(<Probe />);
  await user.click(screen.getByRole('button', { name: 'Refresh' })); await user.click(screen.getByRole('button', { name: 'Write' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Write' })).toBeDisabled());
  finish(); await waitFor(() => expect(screen.getByRole('button', { name: 'Write' })).toBeDisabled()); expect(writes).toBe(1);
});
it('aborts a pending command when its Shop workspace unmounts', async () => {
  let signal: AbortSignal | undefined;
  function Probe() {
    const command = useSettingsCommand(); return <button onClick={() => { void command.run(async current => { signal = current; return new Promise<void>(() => {}); }); }}>Write</button>;
  }
  const { user, unmount } = renderWithProviders(<Probe />); await user.click(screen.getByRole('button', { name: 'Write' }));
  expect(signal?.aborted).toBe(false); unmount(); expect(signal?.aborted).toBe(true);
});
