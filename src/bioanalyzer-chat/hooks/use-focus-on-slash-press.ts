'use client';
import { useEffect, useRef } from 'react';

function useFocusOnSlashPress<T extends HTMLInputElement | HTMLTextAreaElement>(): React.RefObject<T | null> {
  const inputRef = useRef<T>(null);
  useEffect(() => {
    const handleSlashKeyDown = (e: KeyboardEvent) => {
      if (e.key === '/' && !isInputElement(e.target)) {
        inputRef.current?.focus();
      }
    };

    document.addEventListener('keydown', handleSlashKeyDown);

    return () => document.removeEventListener('keydown', handleSlashKeyDown);
  }, []);

  // Helper function to check if element is an input or textarea
  function isInputElement(element: EventTarget | null): boolean {
    if (!element) return false;
    return ['INPUT', 'TEXTAREA'].includes((element as HTMLElement).nodeName);
  }
  return inputRef;
}

export default useFocusOnSlashPress;
