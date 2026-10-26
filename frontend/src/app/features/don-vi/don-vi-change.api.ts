import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> { success: boolean; data: T; }

export interface MergeDonViRequest { sourceIds: number[]; targetId: number; note?: string; }
export interface MoveChildDonViItem { childId: number; targetId: number; }
export interface MoveUnitUserItem { userId: number; targetId: number; roleIds: number[]; }
export interface SplitDonViRequest {
  sourceId: number;
  children: MoveChildDonViItem[];
  users: MoveUnitUserItem[];
  deactivateSource: boolean;
  note?: string;
}
export interface DonViChangePreviewDto {
  unitCount: number;
  childCount: number;
  userCount: number;
  draftSnapshotCount: number;
  errors: string[];
}
export interface DonViChangeResultDto { changeId: number; childCount: number; userCount: number; }
export interface DonViVersionDto {
  id: number; donViId: number; maDonVi: string; tenDonVi: string;
  parentId?: number; isActive: boolean; validFrom: string; validTo?: string;
}
export interface DonViSuccessorDto {
  id: number; changeId: number; sourceDvId: number; targetDvId: number; effectiveAt: string;
}

@Injectable({ providedIn: 'root' })
export class DonViChangeApi {
  constructor(private readonly http: HttpClient) {}
  private readonly base = `${API_BASE_URL}/don-vi/changes`;

  previewMerge(payload: MergeDonViRequest): Promise<DonViChangePreviewDto> {
    return firstValueFrom(this.http.post<ApiResponse<DonViChangePreviewDto>>(`${this.base}/merge/preview`, payload)).then(x => x.data);
  }
  merge(payload: MergeDonViRequest): Promise<DonViChangeResultDto> {
    return firstValueFrom(this.http.post<ApiResponse<DonViChangeResultDto>>(`${this.base}/merge`, payload)).then(x => x.data);
  }
  previewSplit(payload: SplitDonViRequest): Promise<DonViChangePreviewDto> {
    return firstValueFrom(this.http.post<ApiResponse<DonViChangePreviewDto>>(`${this.base}/split/preview`, payload)).then(x => x.data);
  }
  split(payload: SplitDonViRequest): Promise<DonViChangeResultDto> {
    return firstValueFrom(this.http.post<ApiResponse<DonViChangeResultDto>>(`${this.base}/split`, payload)).then(x => x.data);
  }
  versions(unitId: number): Promise<DonViVersionDto[]> {
    return firstValueFrom(this.http.get<ApiResponse<DonViVersionDto[]>>(`${this.base}/${unitId}/versions`)).then(x => x.data);
  }
  successors(unitId: number): Promise<DonViSuccessorDto[]> {
    return firstValueFrom(this.http.get<ApiResponse<DonViSuccessorDto[]>>(`${this.base}/${unitId}/successors`)).then(x => x.data);
  }
}
