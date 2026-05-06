import {
  AfterViewInit,
  Directive,
  ElementRef,
  Input,
  OnChanges,
  Renderer2,
  SimpleChanges,
} from '@angular/core';

@Directive({
  selector: 'p-checkbox[indeterminate]',
  standalone: true,
})
export class CheckboxIndeterminateDirective
  implements AfterViewInit, OnChanges
{
  @Input() indeterminate = false;

  constructor(
    private readonly elementRef: ElementRef<HTMLElement>,
    private readonly renderer: Renderer2,
  ) {}

  ngAfterViewInit(): void {
    this.applyIndeterminateState();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!('indeterminate' in changes)) {
      return;
    }

    this.applyIndeterminateState();
  }

  private applyIndeterminateState(): void {
    const host = this.elementRef.nativeElement;
    const input = host.querySelector<HTMLInputElement>(
      'input[type="checkbox"]',
    );
    const box = host.querySelector<HTMLElement>('.p-checkbox-box');

    if (!input || !box) {
      return;
    }

    input.indeterminate = this.indeterminate;
    input.setAttribute(
      'aria-checked',
      this.indeterminate ? 'mixed' : `${input.checked}`,
    );

    if (this.indeterminate) {
      this.renderer.addClass(box, 'p-indeterminate');
      this.renderer.setAttribute(box, 'data-p-indeterminate', 'true');
      return;
    }

    this.renderer.removeClass(box, 'p-indeterminate');
    this.renderer.removeAttribute(box, 'data-p-indeterminate');
  }
}
