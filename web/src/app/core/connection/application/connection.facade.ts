import { computed, inject, Injectable } from '@angular/core';
import type { OutgoingMessage } from '../domain/messages';
import { ConnectionPort } from './connection.port';

/** What pages use to read the connection state and talk to the agent. Components never see the socket. */
@Injectable({ providedIn: 'root' })
export class ConnectionFacade {
  private readonly port = inject(ConnectionPort);

  readonly status = this.port.status;
  readonly connected = computed(() => this.status() === 'connected');
  readonly messages = this.port.messages;

  start(): void {
    this.port.connect();
  }

  send(message: OutgoingMessage): boolean {
    return this.port.send(message);
  }
}
