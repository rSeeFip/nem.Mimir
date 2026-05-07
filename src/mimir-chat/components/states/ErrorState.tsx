import React from 'react';

interface ErrorStateProps {
  message?: string;
  onRetry?: () => void;
}

export const ErrorState: React.FC<ErrorStateProps> = ({ 
  message = 'An error occurred while loading data.', 
  onRetry 
}) => {
  return (
    <div className="flex flex-col items-center justify-center py-20">
      <div className="bg-red-500/10 border border-red-500 text-red-500 p-6 rounded-lg max-w-md w-full text-center">
        <h3 className="font-semibold text-lg mb-2">Error</h3>
        <p className="text-sm mb-4">{message}</p>
        {onRetry && (
          <button 
            onClick={onRetry}
            className="px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600 transition-colors"
          >
            Retry
          </button>
        )}
      </div>
    </div>
  );
};
