import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private connection: HubConnection | null = null;
  readonly state = signal<HubConnectionState>(HubConnectionState.Disconnected);

  async connect(hubName = 'events', accessToken?: string): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    const url = `${environment.hubBaseUrl}/${hubName}`;
    this.connection = new HubConnectionBuilder()
      .withUrl(url, accessToken ? { accessTokenFactory: () => accessToken } : {})
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
      .build();

    this.connection.onreconnecting(() => this.state.set(HubConnectionState.Reconnecting));
    this.connection.onreconnected(() => this.state.set(HubConnectionState.Connected));
    this.connection.onclose(() => this.state.set(HubConnectionState.Disconnected));

    this.state.set(HubConnectionState.Connecting);
    await this.connection.start();
    this.state.set(HubConnectionState.Connected);
  }

  on<T>(eventName: string, handler: (payload: T) => void): () => void {
    this.connection?.on(eventName, handler);
    return () => this.connection?.off(eventName, handler);
  }

  async disconnect(): Promise<void> {
    await this.connection?.stop();
    this.connection = null;
    this.state.set(HubConnectionState.Disconnected);
  }
}
