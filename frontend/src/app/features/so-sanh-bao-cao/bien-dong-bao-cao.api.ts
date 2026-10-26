import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> { success: boolean; data: T; }
export interface ChangeUnitOption { id: number; parentId?: number; maDonVi: string; tenDonVi: string; hasReportData: boolean; }
export interface ChangePeriodOption { id: number; kyCode: string; tenKy: string; mauBaoCaoId?: number; tenMau: string; nam: number; quy?: number; thang?: number; moduleCodes: string[]; }
export interface ChangeModuleOption { code: string; name: string; }
export interface ChangeOptions { canViewScope: boolean; currentDonViId: number; donVis: ChangeUnitOption[]; kyBaoCaos: ChangePeriodOption[]; modules: ChangeModuleOption[]; }
export type ChangeStatus = 'IMPROVING' | 'WORSENING' | 'STABLE' | 'CHANGED' | 'MISSING';
export interface ChangeMetric { metricCode: string; metricName: string; unit: string; direction: string; isPrimary: boolean; fromValue?: number; toValue?: number; delta?: number; deltaPercent?: number; status: ChangeStatus; }
export interface ChangeModule { moduleCode: string; moduleName: string; status: ChangeStatus; metrics: ChangeMetric[]; }
export interface ChangeUnit { donViId: number; maDonVi: string; tenDonVi: string; status: ChangeStatus; improvingCount: number; worseningCount: number; changedCount: number; missingCount: number; modules: ChangeModule[]; }
export interface ChangeOverview { fromKyBaoCaoId: number; fromKyCode: string; toKyBaoCaoId: number; toKyCode: string; totalUnits: number; improvingCount: number; worseningCount: number; stableCount: number; changedCount: number; missingDataCount: number; units: ChangeUnit[]; }
export interface CurrentMetric { metricCode: string; metricName: string; unit: string; isPrimary: boolean; value?: number; }
export interface CurrentModule { moduleCode: string; moduleName: string; metrics: CurrentMetric[]; }
export interface CurrentUnit { donViId: number; maDonVi: string; tenDonVi: string; moduleCount: number; metricCount: number; modules: CurrentModule[]; }
export interface CurrentOverview { kyBaoCaoId: number; kyCode: string; tenKy: string; totalUnits: number; totalModules: number; totalMetrics: number; units: CurrentUnit[]; }
export type RecordCell = string | number | boolean | null;
export interface ModuleRecordSection { title: string; headers: string[]; rows: RecordCell[][]; }
export interface ModuleRecords { moduleCode: string; moduleName: string; sections: ModuleRecordSection[]; }

@Injectable({ providedIn: 'root' })
export class BienDongBaoCaoApi {
  constructor(private readonly http: HttpClient) {}

  getOptions(): Promise<ChangeOptions> {
    return firstValueFrom(this.http.get<ApiResponse<ChangeOptions>>(`${API_BASE_URL}/bien-dong-bao-cao/options`)).then((response) => response.data);
  }

  getOverview(toKyBaoCaoId: number, fromKyBaoCaoId?: number | null, donViId?: number | null, moduleCode?: string | null): Promise<ChangeOverview> {
    let params = new HttpParams().set('toKyBaoCaoId', toKyBaoCaoId.toString());
    if (fromKyBaoCaoId != null) params = params.set('fromKyBaoCaoId', fromKyBaoCaoId.toString());
    if (donViId != null) params = params.set('donViId', donViId.toString());
    if (moduleCode) params = params.set('moduleCode', moduleCode);
    return firstValueFrom(this.http.get<ApiResponse<ChangeOverview>>(`${API_BASE_URL}/bien-dong-bao-cao/overview`, { params })).then((response) => response.data);
  }

  getCurrent(kyBaoCaoId: number, donViId?: number | null, moduleCode?: string | null): Promise<CurrentOverview> {
    let params = new HttpParams().set('kyBaoCaoId', kyBaoCaoId.toString());
    if (donViId != null) params = params.set('donViId', donViId.toString());
    if (moduleCode) params = params.set('moduleCode', moduleCode);
    return firstValueFrom(this.http.get<ApiResponse<CurrentOverview>>(`${API_BASE_URL}/bien-dong-bao-cao/current`, { params })).then((response) => response.data);
  }

  getModuleRecords(kyBaoCaoId: number, donViId: number, moduleCode: string): Promise<ModuleRecords> {
    const params = new HttpParams().set('kyBaoCaoId', kyBaoCaoId.toString()).set('donViId', donViId.toString()).set('moduleCode', moduleCode);
    return firstValueFrom(this.http.get<ApiResponse<ModuleRecords>>(`${API_BASE_URL}/bien-dong-bao-cao/records`, { params })).then((response) => response.data);
  }
}
