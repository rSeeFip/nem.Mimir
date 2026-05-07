import React from 'react';
import { IconDatabaseOff } from '@tabler/icons-react';

interface EmptyStateProps {
  message?: string;
  icon?: React.ReactNode;
}

export const EmptyState: React.FC<EmptyStateProps> = ({ 
  message = 'No data found.', 
  icon = <IconDatabaseOff size={48} /> 
}) => {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-gray-400">
      <div className="mb-4 opacity-50">
        {icon}
      </div>
      <p className="text-lg">{message}</p>
    </div>
  );
};
