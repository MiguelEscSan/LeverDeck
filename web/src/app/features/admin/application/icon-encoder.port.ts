/** Turns a picture the owner picked into the icon the agent stores: a square PNG, as base64. */
export abstract class IconEncoder {
  /** Rejects with a message to show when the file is not a picture the browser can read. */
  abstract encode(file: Blob): Promise<string>;
}
