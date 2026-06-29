export interface JwtPayload {
  exp?: number;
  [key: string]: any;
}

export function parseJwt(token: string): JwtPayload | null {
  try {
    const base64Url = token.split('.')[1];
    if (!base64Url) return null;
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      window
        .atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch (error) {
    return null;
  }
}

export function getTokenExpiration(token: string | null): number {
  if (!token) return 0;
  const payload = parseJwt(token);
  if (!payload || !payload.exp) return 0;
  const currentTime = Math.floor(Date.now() / 1000); 
  return payload.exp - currentTime
}

export function isTokenExpired(token: string | null): boolean {
  const expiresIn = getTokenExpiration(token)
  if (!expiresIn) return true
  return expiresIn <= 0
}

