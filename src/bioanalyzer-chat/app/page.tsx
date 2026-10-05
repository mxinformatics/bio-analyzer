'use client';
import React from 'react';
import { signIn, useSession } from "next-auth/react";
import {useRouter} from 'next/navigation';
import { useEffect } from "react";
//import { DefaultChatTransport } from 'ai';
import { useChat } from '@ai-sdk/react';
import useEnterSubmit from '@/hooks/use-enter-submit';
import useFocusOnSlashPress from '@/hooks/use-focus-on-slash-press';
import ChatList from '@/components/chat/ChatList';
import { Textarea } from '@/components/ui/textarea';
import AutoScroll from '@/components/AutoScroll';
import { set } from 'zod';

export default function Home() {

  const { data: session, status: sessionStatus } = useSession();
  const router = useRouter();
  const { formRef, onKeyDown } = useEnterSubmit();
  const inputRef = useFocusOnSlashPress<HTMLTextAreaElement>();
  const [input, setInput] = React.useState('');
  

  useEffect(() => {
    if (sessionStatus === 'unauthenticated') {
      void signIn('azure-ad');
    }
  }, [sessionStatus, session, router]);

  // const transport = React.useMemo(
  //   () => new DefaultChatTransport({
  //     api: '/api/chat'
  //   }),
  //   []
  // );

  const {messages, sendMessage, status: chatStatus} = useChat({

  });

 

  const handleSubmit = (e: { preventDefault: () => void; }) => {
    e.preventDefault();
    if (!input.trim()) return;
    sendMessage({text: input});
    setInput('');
  }

  const isLoading = chatStatus === 'submitted';
  const messageEndRef = React.useRef<HTMLDivElement | null>(null);
  const scrollToBottom = () => {
    messageEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  React.useEffect(() => {
    scrollToBottom();
  }, [messages]);

  if (!session) {
    return <div>...</div>;
  };

  return (
<>
  <div className="flex flex-col w-full max-w-4xl mx-auto py-24 mx-auto stretch">
      {messages.length === 0 && (
        <h1 className="text-6xl font-semibold leading-tight mt-4 mb-16">
          <div className="inline-block">Hello, I am the ✴️ BioAnalyzer AI</div>
          <br />
          <span className="text-gray-400">Ask me anything you want</span>
        </h1>
      )}
      {messages.length > 0 && <ChatList messages={messages} isLoading={isLoading} />}
      <div ref={messageEndRef}></div>
      <form
        className="stretch max-w-4xl flex flex-row"
        ref={formRef}
        role="form"
        aria-labelledby="chat-form-label"
        onSubmit={handleSubmit}
      >
        <Textarea
          ref={inputRef}
          className="fixed bottom-0 w-full max-w-4xl p-2 mb-8 border border-gray-300 rounded shadow-xl"
          placeholder="Type your message here..."
          tabIndex={0}
          autoFocus
          spellCheck={false}
          autoComplete="off"
          autoCorrect="off"
          name="message"
          rows={1}
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={onKeyDown} />
      </form>
      <AutoScroll trackVisibility />
    </div></>

  );
}
