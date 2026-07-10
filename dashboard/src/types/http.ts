export interface HttpRequestEntry {
  timestamp: string;
  id: string;
  completed: boolean;
  inbound: string;
  requestURI: string;
  requestMethod: string;
  route: string;
  targetHost: string;
  headers: Record<string, string>;
}

export interface HttpRequestPayload {
  entry: HttpRequestEntry;
  maxEntries: number;
}

export interface HttpResponseEntry {
  timestamp: string;
  id: string;
  completed: boolean;
  inbound: string;
  statusCode: string;
  statusText: string;
  type: string;
  size: number | null;
  route: string;
  targetHost: string;
  headers: Record<string, string>;
}

export interface HttpResponsePayload {
  entry: HttpResponseEntry;
  maxEntries: number;
}

export interface HttpCompletionEntry {
  timestamp: string;
  id: string;
}

export interface HttpCompletionPayload {
  entry: HttpCompletionEntry;
  maxEntries: number;
}

export type HttpBodyEntry = HttpMultipartPartEntry & {
  timestamp: string;
  id: string;
  completed: boolean;
  compressedLength: number | null;
};

export interface HttpBodyPayload {
  entry: HttpBodyEntry;
  maxEntries: number;
}

export type HttpMultipartPartEntry =
  | HttpJsonBodyEntry
  | HttpXmlBodyEntry
  | HttpHtmlBodyEntry
  | HttpTextBodyEntry
  | HttpImageBodyEntry
  | HttpBinaryBodyEntry
  | HttpFormUrlEncodedBodyEntry
  | HttpMultipartBodyEntry
  | HttpJavascriptBodyEntry
  | HttpTypescriptBodyEntry
  | HttpMicrosoftAjaxDeltaBodyEntry;

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
  MultipartFormData = 7,
  Javascript = 8,
  Typescript = 9,
  MicrosoftAjaxDelta = 10,
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

export interface HttpJavascriptBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.Javascript;
  content: string;
}

export interface HttpTypescriptBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.Typescript;
  content: string;
}

export interface HttpMicrosoftAjaxDeltaBodyEntry
  extends HttpMultipartPartBaseEntry {
  contentKind: HttpContentKind.MicrosoftAjaxDelta;
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
  type: string;
  size: number | null;
  durationMs: number | null;
  completed: boolean;
  requestHeaders: Record<string, string>;
  responseHeaders: Record<string, string>;
  requestBody: HttpBodyEntry | null;
  responseBody: HttpBodyEntry | null;
}

export interface HttpHistoryDto {
  requests: HttpRequestEntry[];
  responses: HttpResponseEntry[];
  completions: HttpCompletionEntry[];
  requestBodies: HttpBodyEntry[];
  responseBodies: HttpBodyEntry[];
}
