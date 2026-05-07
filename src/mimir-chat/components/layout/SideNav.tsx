import React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/router';
import { 
  IconMessage, 
  IconHistory, 
  IconSettings, 
  IconReceipt, 
  IconShieldLock 
} from '@tabler/icons-react';

export const SideNav: React.FC = () => {
  const router = useRouter();

  const navItems = [
    { label: 'Chat', href: '/', icon: <IconMessage size={20} /> },
    { label: 'History', href: '/history', icon: <IconHistory size={20} /> },
    { label: 'Settings', href: '/settings', icon: <IconSettings size={20} /> },
    { label: 'Billing', href: '/billing', icon: <IconReceipt size={20} /> },
    { label: 'Admin', href: '/admin', icon: <IconShieldLock size={20} /> },
  ];

  return (
    <nav className="w-64 bg-[#202123] flex flex-col border-r border-white/20 h-full">
      <div className="p-4 flex items-center justify-center border-b border-white/20">
        <h2 className="text-xl font-bold text-white">nem.Mimir</h2>
      </div>
      <div className="flex-1 py-4 flex flex-col gap-2 px-2">
        {navItems.map((item) => {
          const isActive = router.pathname === item.href || (item.href !== '/' && router.pathname.startsWith(item.href));
          return (
            <Link 
              key={item.href} 
              href={item.href}
              className={`flex items-center gap-3 px-3 py-3 rounded-md transition-colors ${
                isActive 
                  ? 'bg-[#343541] text-white' 
                  : 'text-gray-300 hover:bg-[#2A2B32] hover:text-white'
              }`}
            >
              {item.icon}
              <span>{item.label}</span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
};
