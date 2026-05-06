export interface AuthToken {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserProfile;
}

export interface UserProfile {
  userId: number;
  username: string;
  hoTen: string;
  donViId: number;
  roles: string[];
  permissions: string[];
}

export interface ApiResponse<T> {
  success: boolean;
  data: T;
}

/// Level-3: Refresh Token Management
export interface RefreshTokenRequest {
  deviceId: string;
  deviceUserAgent: string;
  deviceName?: string;
}

export interface RefreshTokenResponse {
  accessToken: string;
  expiresIn: number;
  user?: UserProfile;
}

export interface Session {
  id: number;
  deviceId: string;
  deviceName?: string;
  deviceUserAgent: string;
  deviceIpAddress: string;
  issuedAt: string;
  expiresAt: string;
  lastUsedAt?: string;
  isCurrentSession: boolean;
  isRevoked: boolean;
}

export interface RevokeSessionRequest {
  sessionId?: number;
  revocationReason?: string;
}
