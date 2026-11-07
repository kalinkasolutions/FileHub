import { HttpParameterCodec, HttpParams } from '@angular/common/http';

/**
 * Angular's default codec leaves `+`, `;`, `=`, `?` and `/` in a query value as they are, and the
 * server reads a `+` as a space — so a folder called "Heat + DLC" arrives as "Heat   DLC" and is
 * not found. This one encodes everything `encodeURIComponent` does.
 */
class StrictQueryCodec implements HttpParameterCodec {
  public encodeKey(key: string): string {
    return encodeURIComponent(key);
  }

  public encodeValue(value: string): string {
    return encodeURIComponent(value);
  }

  public decodeKey(key: string): string {
    return decodeURIComponent(key);
  }

  public decodeValue(value: string): string {
    return decodeURIComponent(value);
  }
}

/** Query parameters that survive a `+` in a value. Use this, not `new HttpParams()`. */
export function queryParams(values: Record<string, string> = {}): HttpParams {
  return new HttpParams({ encoder: new StrictQueryCodec(), fromObject: values });
}
