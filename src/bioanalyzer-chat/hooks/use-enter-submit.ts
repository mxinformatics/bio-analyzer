'use client';
import { useRef } from 'react';

interface UseEnterSubmitReturn {
  formRef: React.RefObject<HTMLFormElement | null>;
  onKeyDown: (event: React.KeyboardEvent<HTMLTextAreaElement>) => void;
}

function useEnterSubmit(): UseEnterSubmitReturn {
  const formRef = useRef<HTMLFormElement>(null);
  const handleKeyDown = (event: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      formRef.current?.requestSubmit();
      event.preventDefault();
    }
  };

  return { formRef, onKeyDown: handleKeyDown };
}

export default useEnterSubmit;
