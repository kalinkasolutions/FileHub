import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { ILogPage, ILogQuery } from '@models/ILogEntry';
import { Observable } from 'rxjs';

/** Where the hub is mapped. Under `/api` so the forced-password-change gate covers it. */
const hubUrl = '/api/admin/logs/stream';

/** The one message the hub sends. Must match `LogHub.LoggedMessage` on the server. */
const loggedMessage = 'logged';

/**
 * The server's own log, for the admin log screen. Admin-only on the API as well — the log names
 * accounts, base paths and file names, so it is the most revealing read in the application.
 */
@Injectable({ providedIn: 'root' })
export class LogService {
  private readonly http = inject(HttpClient);

  public query(query: ILogQuery): Observable<ILogPage> {
    return this.http.get<ILogPage>('/api/admin/logs', { params: toParams(query) });
  }

  /**
   * Opens the live channel and calls `onLogged` whenever the server says something was written.
   *
   * The hub sends a bare signal, not the entries — see `LogHub` on the server. The caller answers
   * it with an ordinary `query({ afterId })`, so the filter and the ids stay in one place and a
   * missed signal costs nothing: the next one finds the lines.
   *
   * `onState` reports the connection's own health, because a live view that has quietly stopped
   * being live is worse than one that says so.
   *
   * Returns a stop function; there is no shared connection to leak because each caller gets its own.
   */
  public connect(onLogged: () => void, onState: (state: LogStreamState) => void): () => void {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      // The session is the auth: the cookie rides along, same-origin, exactly like every other call
      // the SPA makes. No token to pass and none to leak.
      .withAutomaticReconnect()
      // The default is Information, which puts SignalR's own chatter in the browser console on every
      // page with the log screen open.
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on(loggedMessage, onLogged);
    connection.onreconnecting(() => onState('reconnecting'));
    connection.onreconnected(() => {
      onState('live');
      // Anything logged while the socket was down is still in the table; ask for it rather than
      // waiting for the next thing to happen.
      onLogged();
    });
    connection.onclose(() => onState('offline'));

    connection
      .start()
      .then(() => onState('live'))
      .catch(() => onState('offline'));

    return () => {
      // Take the handler off first: a message arriving mid-teardown would otherwise reach a
      // component that is already gone.
      connection.off(loggedMessage);
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop();
      }
    };
  }
}

/** What the log screen shows about its own connection. */
export type LogStreamState = 'connecting' | 'live' | 'reconnecting' | 'offline';

/**
 * Only the fields that are actually set. An empty `search` sent as `search=` is a filter the server
 * would have to decide to ignore; leaving it off means "no filter" says so on the wire.
 */
function toParams(query: ILogQuery): HttpParams {
  let params = new HttpParams();

  if (query.minLevel) {
    params = params.set('minLevel', query.minLevel);
  }

  if (query.search) {
    params = params.set('search', query.search);
  }

  if (query.from) {
    params = params.set('from', query.from);
  }

  if (query.to) {
    params = params.set('to', query.to);
  }

  // `> 0` rather than truthiness by accident: id 0 is not a real row, but writing it this way makes
  // the intent explicit next to the `afterId` semantics.
  if (query.afterId !== undefined && query.afterId > 0) {
    params = params.set('afterId', String(query.afterId));
  }

  if (query.take !== undefined) {
    params = params.set('take', String(query.take));
  }

  return params;
}
