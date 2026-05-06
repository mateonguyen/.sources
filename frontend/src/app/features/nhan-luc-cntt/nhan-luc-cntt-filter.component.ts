import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { APP_SELECT_PANEL_STYLE_CLASS } from '../../shared/ui/primeng-pt';
import { SelectOption } from './nhan-luc-cntt.models';

@Component({
  selector: 'app-nhan-luc-cntt-filter',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    DropdownModule,
    ButtonModule,
  ],
  templateUrl: './nhan-luc-cntt-filter.component.html',
  styleUrl: './nhan-luc-cntt-filter.component.scss',
})
export class NhanLucCnttFilterComponent {
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;

  @Input({ required: true }) form!: FormGroup;
  @Input() loading = false;
  @Input() advancedVisible = false;
  @Input() advancedFilterCount = 0;
  @Input() namOptions: SelectOption<number | null>[] = [];
  @Input() donViOptions: SelectOption<number | null>[] = [];
  @Input() gioiTinhOptions: SelectOption<string | null>[] = [];
  @Input() capBacOptions: SelectOption<string | null>[] = [];
  @Input() loaiNhanLucOptions: SelectOption<string | null>[] = [];
  @Input() trinhDoCnttOptions: SelectOption<string | null>[] = [];

  @Output() apply = new EventEmitter<void>();
  @Output() clear = new EventEmitter<void>();
  @Output() toggleAdvanced = new EventEmitter<void>();

  get typedForm(): FormGroup {
    return this.form;
  }

  get advancedFilterLabel(): string {
    return this.advancedFilterCount > 0
      ? `Bộ lọc nâng cao (${this.advancedFilterCount})`
      : 'Bộ lọc nâng cao';
  }
}
