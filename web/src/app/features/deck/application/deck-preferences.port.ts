/** What the deck remembers on this device between visits (`localStorage` in the browser). Never throws. */
export abstract class DeckPreferences {
  abstract lastProfileId(): string | null;
  abstract rememberProfile(profileId: string): void;
}
