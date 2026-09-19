import { describe, beforeEach, it, expect } from 'vitest';
import { App } from './app';

describe('App Component (Unit Tests)', () => {
  let app: App;

  beforeEach(() => {
    app = new App();
  });

  it('should create the app shell instance', () => {
    expect(app).toBeTruthy();
  });
});
