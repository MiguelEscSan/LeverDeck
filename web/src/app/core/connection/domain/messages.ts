import type {
  AckMessage,
  CommandMessage,
  ErrorMessage,
  EventMessage,
  QueryMessage,
  QueryResultMessage,
} from '../../protocol';

/** Messages the agent sends to a device (ROADMAP 3.2). */
export type IncomingMessage = AckMessage | ErrorMessage | QueryResultMessage | EventMessage;

/** Messages a device sends to the agent (ROADMAP 3.2). */
export type OutgoingMessage = CommandMessage | QueryMessage;
