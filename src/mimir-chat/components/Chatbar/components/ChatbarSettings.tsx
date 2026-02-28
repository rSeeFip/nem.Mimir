import { IconFileExport, IconLogout, IconSettings, IconUser } from '@tabler/icons-react';
import { useContext, useState } from 'react';

import { useTranslation } from 'next-i18next';

import { useSession, signOut } from 'next-auth/react';

import HomeContext from '@/pages/api/home/home.context';

import { SettingDialog } from '@/components/Settings/SettingDialog';

import { Import } from '../../Settings/Import';

import { SidebarButton } from '../../Sidebar/SidebarButton';
import ChatbarContext from '../Chatbar.context';
import { ClearConversations } from './ClearConversations';
import { PluginKeys } from './PluginKeys';

export const ChatbarSettings = () => {
  const { t } = useTranslation('sidebar');
  const [isSettingDialogOpen, setIsSettingDialog] = useState<boolean>(false);
  const { data: session } = useSession();


  const {
    state: {

      serverSidePluginKeysSet,
      conversations,
    },

  } = useContext(HomeContext);

  const {
    handleClearConversations,
    handleImportConversations,
    handleExportData,

  } = useContext(ChatbarContext);

  return (
    <>
    <div className="flex flex-col items-center space-y-1 border-t border-white/20 pt-1 text-sm">
      {conversations.length > 0 ? (
        <ClearConversations onClearConversations={handleClearConversations} />
      ) : null}

      <Import onImport={handleImportConversations} />

      <SidebarButton
        text={t('Export data')}
        icon={<IconFileExport size={18} />}
        onClick={() => handleExportData()}
      />

      <SidebarButton
        text={t('Settings')}
        icon={<IconSettings size={18} />}
        onClick={() => setIsSettingDialog(true)}
      />


      {!serverSidePluginKeysSet ? <PluginKeys /> : null}

      <SettingDialog
        open={isSettingDialogOpen}
        onClose={() => {
          setIsSettingDialog(false);
        }}
      />
    </div>

    {/* User profile & logout */}
    <div className="flex w-full items-center gap-3 border-t border-white/20 pt-2 mt-1 px-3 pb-1">
      <IconUser size={18} className="shrink-0 text-neutral-400" />
      <div className="flex flex-col overflow-hidden text-xs">
        {session?.user?.name && (
          <span className="truncate font-medium text-white">{session.user.name}</span>
        )}
        {session?.user?.email && (
          <span className="truncate text-neutral-400">{session.user.email}</span>
        )}
      </div>
      <button
        className="ml-auto shrink-0 text-neutral-400 hover:text-neutral-100"
        onClick={() => signOut()}
        title={t('Log out') || 'Log out'}
      >
        <IconLogout size={18} />
      </button>
    </div>
    </>
  );
};
