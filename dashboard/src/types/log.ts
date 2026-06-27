export interface LogEntry {
  timestamp: string;
  category: string;
  level: 'Debug' | 'Information' | 'Warning' | 'Error' | 'Critical';
  message: string;
  exception?: string | null;
}

export interface HttpRequestEntry {
  timestamp: string;
  id: string;
  inbound: string;
  requestURI: string;
  requestMethod: string;
  route: string;
  targetHost: string;
  headers: Record<string, string>;
}

export interface HttpResponseEntry {
  timestamp: string;
  id: string;
  inbound: string;
  statusCode: string;
  statusText: string;
  route: string;
  targetHost: string;
  headers: Record<string, string>;
}

export type HttpBodyEntry = HttpMultipartPartEntry & {
  id: string;
};

export type HttpMultipartPartEntry =
  | HttpJsonBodyEntry
  | HttpXmlBodyEntry
  | HttpHtmlBodyEntry
  | HttpTextBodyEntry
  | HttpImageBodyEntry
  | HttpBinaryBodyEntry
  | HttpFormUrlEncodedBodyEntry
  | HttpMultipartBodyEntry;

interface HttpMultipartPartBaseEntry {
  id: string;
  length: number;
  contentType?: string | null;
  name?: string | null;
  fileName?: string | null;
}

export enum HttpContentKind {
  Json = 0,
  Xml = 1,
  Html = 2,
  Text = 3,
  Image = 4,
  Binary = 5,
  FormUrlEncoded = 6,
  MultipartFormData = 7
}

export interface HttpJsonBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.Json;
  content: string;
}

export interface HttpXmlBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.Xml;
  content: string;
}

export interface HttpHtmlBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.Html;
  content: string;
}

export interface HttpTextBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.Text;
  content: string;
}

export interface HttpImageBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.Image;
  binaryContentBase64: string;
}

export interface HttpBinaryBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.Binary;
  binaryContentBase64: string;
}

export interface HttpFormUrlEncodedBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.FormUrlEncoded;
  content: string;
}

export interface HttpMultipartBodyEntry 
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.MultipartFormData;
  parts?: HttpMultipartPartEntry[] | null;
}

// Unified state for display row mapping
export interface MergedTrafficEntry {
  id: string;
  timestamp: string;
  inbound: string;
  requestURI: string;
  requestMethod: string;
  route: string;
  targetHost: string;
  statusCode: number | null;
  statusText: string;
  durationMs: number | null;
  requestHeaders: Record<string, string>;
  responseHeaders: Record<string, string>;
  requestBody: HttpBodyEntry | null;
  responseBody: HttpBodyEntry | null;
}

export interface HttpHistoryDto {
  requests: HttpRequestEntry[];
  responses: HttpResponseEntry[];
  requestBodies: HttpBodyEntry[];
  responseBodies: HttpBodyEntry[];
}