import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly activeRequests = signal(0);
  readonly isLoading = signal(false);

  begin(): void {
    this.activeRequests.update((value) => value + 1);
    this.isLoading.set(true);
  }

  end(): void {
    this.activeRequests.update((value) => Math.max(0, value - 1));
    this.isLoading.set(this.activeRequests() > 0);
  }
}
