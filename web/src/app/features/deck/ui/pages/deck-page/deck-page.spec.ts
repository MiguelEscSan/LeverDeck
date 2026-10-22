import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import type { ConnectionStatus } from '../../../../../core/connection/domain/connection-status';
import type { ConnectionRejection } from '../../../../../core/connection/domain/rejection';
import type { ButtonDto, ProfileDto } from '../../../../../core/protocol';
import { DeckFacade } from '../../../application/deck.facade';
import type { DeckCell } from '../../../domain/deck';
import { DeckPage } from './deck-page';

@Component({ changeDetection: ChangeDetectionStrategy.OnPush, template: 'pair' })
class PairStub {}

const play: ButtonDto = {
  id: 'play',
  row: 0,
  column: 0,
  label: 'Play',
  color: 'dark',
  iconId: 'play-pause',
  action: { type: 'mediaKey', key: 'playPause' },
};

const profile: ProfileDto = {
  id: 'p1',
  name: 'Default',
  color: 'teal',
  grid: { columns: 1, rows: 1 },
  pages: [{ index: 0, buttons: [play] }, { index: 1, buttons: [] }],
};

class FakeDeck {
  readonly rejection = signal<ConnectionRejection | null>(null);
  readonly status = signal<ConnectionStatus>('connected');
  readonly profiles = signal([{ id: 'p1', name: 'Default', color: 'teal' as const }]);
  readonly profile = signal<ProfileDto | null>(profile);
  readonly cells = signal<DeckCell[]>([{ row: 0, column: 0, button: play }]);
  readonly page = signal(0);
  readonly pageCount = signal(2);
  readonly feedback = signal({});
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly pressed: ButtonDto[] = [];
  paired = true;

  start(): boolean {
    return this.paired;
  }

  press(button: ButtonDto): void {
    this.pressed.push(button);
  }

  goToPage(): void {
    // Not needed here.
  }

  selectProfile(): void {
    // Not needed here.
  }
}

describe('DeckPage', () => {
  let deck: FakeDeck;

  beforeEach(() => {
    deck = new FakeDeck();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'deck', component: DeckPage },
          { path: 'pair', component: PairStub },
        ]),
        { provide: DeckFacade, useValue: deck },
      ],
    });
  });

  const open = async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/deck');
    await harness.fixture.whenStable();
    return harness;
  };

  it('shows the grid, the profile and the page dots, and sends presses to the facade', async () => {
    const harness = await open();
    const element = harness.routeNativeElement as HTMLElement;

    expect(element.querySelector('select')?.value).toBe('p1');
    expect(element.querySelectorAll('app-page-dots button')).toHaveLength(2);
    element.querySelector<HTMLButtonElement>('app-deck-key button')?.click();

    expect(deck.pressed).toEqual([play]);
  });

  it.each(['unauthorized', 'deviceRevoked'] as const)(
    'goes back to pairing when the agent answers %s',
    async (rejection) => {
      const harness = await open();

      deck.rejection.set(rejection);
      await harness.fixture.whenStable();

      expect(TestBed.inject(Router).url).toBe(`/pair?reason=${rejection}`);
    },
  );

  it('asks for a reload on a protocol mismatch', async () => {
    deck.rejection.set('protocolMismatch');
    const harness = await open();

    expect((harness.routeNativeElement as HTMLElement).textContent).toMatch(/Reload the page/);
  });

  it('goes to pairing when the device is not paired', async () => {
    deck.paired = false;
    await open();
    await TestBed.inject(Router).navigated;

    expect(TestBed.inject(Router).url).toBe('/pair');
  });
});
