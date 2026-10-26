import { UserProfile } from '../auth/auth.models';

export const DEFAULT_QUICK_ROUTES: readonly string[] = [
  '/tong-hop-du-lieu', '/tra-cuu-bao-cao', '/nguoi-dung', '/don-vi',
  '/tien-do-nop', '/nhan-luc-cntt', '/thiet-bi-cntt',
  '/he-thong-thong-tin', '/ky-bao-cao', '/mau-bao-cao',
];

export function quickLinkStorageKey(profile: UserProfile | null): string {
  return `thuc_luc_quick_links_${profile?.userId ?? 'unknown'}`;
}

export function loadQuickLinkRoutes(profile: UserProfile | null): string[] | null {
  try {
    const stored = localStorage.getItem(quickLinkStorageKey(profile));
    if (stored === null) return null;
    const parsed: unknown = JSON.parse(stored);
    return Array.isArray(parsed) && parsed.every((route) => typeof route === 'string')
      ? parsed
      : null;
  } catch {
    return null;
  }
}
