import React, { ReactNode } from 'react';
import { SideNav } from './SideNav';

interface AppShellProps {
  children: ReactNode;
}

export const AppShell: React.FC<AppShellProps> = ({ children }) => {
  return (
    <div className="flex h-screen w-screen bg-[#343541] text-white overflow-hidden">
      <SideNav />
      <main className="flex-1 flex flex-col h-full overflow-hidden">
        {children}
      </main>
    </div>
  );
};
