export interface LogEntry {
  timestamp: string;
  category: string;
  level: 'Debug' | 'Information' | 'Warning' | 'Error' | 'Critical';
  message: string;
  exception?: string | null;
}