import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { TOKEN_STORE } from '../auth/token-store';

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly tokenStore = inject(TOKEN_STORE, { optional: true });
  private connection: HubConnection | null = null;
  private readonly handlers = new Map<string, Set<(payload: any) => void>>();
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  readonly state = signal<HubConnectionState>(HubConnectionState.Disconnected);

  async connect(hubName = 'notifications', accessToken?: string): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    const url = `${environment.hubBaseUrl}/${hubName}`;
    this.connection = new HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => this.tokenStore?.getAccessToken() ?? accessToken ?? ''
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    // Attach all registered handlers to this connection
    for (const [eventName, callbacks] of this.handlers.entries()) {
      for (const cb of callbacks) {
        this.connection.on(eventName, cb);
      }
    }

    this.connection.onreconnecting(() => this.state.set(HubConnectionState.Reconnecting));
    this.connection.onreconnected(() => this.state.set(HubConnectionState.Connected));
    this.connection.onclose(() => this.state.set(HubConnectionState.Disconnected));

    this.state.set(HubConnectionState.Connecting);
    try {
      await this.connection.start();
      this.state.set(HubConnectionState.Connected);
    } catch {
      this.state.set(HubConnectionState.Disconnected);
      this.connection = null;
      // Retry after 2.5s in case token refresh was in flight during initial page load
      if (this.retryTimer) clearTimeout(this.retryTimer);
      this.retryTimer = setTimeout(() => {
        const latestToken = this.tokenStore?.getAccessToken();
        if (latestToken) {
          void this.connect(hubName, latestToken);
        }
      }, 2500);
    }
  }

  on<T>(eventName: string, handler: (payload: T) => void): () => void {
    let set = this.handlers.get(eventName);
    if (!set) {
      set = new Set();
      this.handlers.set(eventName, set);
    }
    if (!set.has(handler)) {
      set.add(handler);
      this.connection?.on(eventName, handler);
    }
    return () => {
      set?.delete(handler);
      this.connection?.off(eventName, handler);
    };
  }

  async joinSprint(sprintId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke('JoinSprint', sprintId);
      } catch {
        // silent fail
      }
    }
  }

  async leaveSprint(sprintId: string): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke('LeaveSprint', sprintId);
      } catch {
        // silent fail
      }
    }
  }

  async disconnect(): Promise<void> {
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
    await this.connection?.stop();
    this.connection = null;
    this.state.set(HubConnectionState.Disconnected);
  }
}
