import { Injectable } from '@angular/core';

export interface UserSettings {
  defaultPageSize: 10 | 20 | 50;
  expandMenuOnLoad: boolean;
  enableInAppNotifications: boolean;
}

const STORAGE_KEY = 'thuc_luc_user_settings';

const DEFAULT_SETTINGS: UserSettings = {
  defaultPageSize: 10,
  expandMenuOnLoad: true,
  enableInAppNotifications: true,
};

@Injectable({ providedIn: 'root' })
export class UserSettingsService {
  private settings: UserSettings = this.load();

  get(): UserSettings {
    return { ...this.settings };
  }

  save(settings: UserSettings): void {
    this.settings = { ...settings };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(this.settings));
  }

  reset(): void {
    this.settings = { ...DEFAULT_SETTINGS };
    localStorage.removeItem(STORAGE_KEY);
  }

  getDefaults(): UserSettings {
    return { ...DEFAULT_SETTINGS };
  }

  private load(): UserSettings {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return { ...DEFAULT_SETTINGS };
      }
      return {
        ...DEFAULT_SETTINGS,
        ...(JSON.parse(raw) as Partial<UserSettings>),
      };
    } catch {
      return { ...DEFAULT_SETTINGS };
    }
  }
}
