import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { NhanLucCnttTableRow, SelectOption } from './nhan-luc-cntt.models';

@Component({
  selector: 'app-nhan-luc-cntt-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    TooltipModule,
    PaginatorModule,
    DropdownModule,
    EmptyStateComponent,
    LoadingOverlayComponent,
  ],
  templateUrl: './nhan-luc-cntt-table.component.html',
  styleUrl: './nhan-luc-cntt-table.component.scss',
})
export class NhanLucCnttTableComponent {
  @Input() loading = false;
  @Input() items: NhanLucCnttTableRow[] = [];
  @Input() totalRecords = 0;
  @Input() overallRecords = 0;
  @Input() first = 0;
  @Input() rows = 10;
  @Input() summary = '';
  @Input() pageSizeOptions: SelectOption<number>[] = [];

  @Output() add = new EventEmitter<void>();
  @Output() edit = new EventEmitter<NhanLucCnttTableRow>();
  @Output() delete = new EventEmitter<NhanLucCnttTableRow>();
  @Output() clearFilters = new EventEmitter<void>();
  @Output() pageChange = new EventEmitter<PaginatorState>();
  @Output() rowsChange = new EventEmitter<number>();

  get totalLabel(): string {
    return `Tổng cộng: ${this.overallRecords} nhân lực`;
  }
}
