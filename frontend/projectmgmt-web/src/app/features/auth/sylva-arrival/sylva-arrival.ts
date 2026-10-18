import { Component, ElementRef, EventEmitter, OnDestroy, Output, ViewChild } from '@angular/core';

@Component({
  selector: 'app-sylva-arrival',
  standalone: true,
  templateUrl: './sylva-arrival.html',
  styleUrl: './sylva-arrival.scss'
})
export class SylvaArrivalComponent implements OnDestroy {
  @ViewChild('sceneFrame') private sceneFrame?: ElementRef<HTMLIFrameElement>;
  @Output() readonly enter = new EventEmitter<void>();

  private enterLink: HTMLAnchorElement | null = null;

  onFrameLoad(): void {
    this.detachEnterLink();
    const frame = this.sceneFrame?.nativeElement;
    const document = frame?.contentDocument;
    if (!document) return;

    this.enterLink = document.querySelector<HTMLAnchorElement>('.dock-item--enter');
    this.enterLink?.addEventListener('click', this.onEnterClick);
  }

  ngOnDestroy(): void {
    this.detachEnterLink();
  }

  private readonly onEnterClick = (event: MouseEvent): void => {
    event.preventDefault();
    this.enter.emit();
  };

  private detachEnterLink(): void {
    this.enterLink?.removeEventListener('click', this.onEnterClick);
    this.enterLink = null;
  }
}
