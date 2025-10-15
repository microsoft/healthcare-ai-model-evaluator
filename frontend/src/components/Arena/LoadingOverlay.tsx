import React from 'react';
import { Spinner, SpinnerSize, Stack } from '@fluentui/react';

interface LoadingOverlayProps {
  label?: string;
}

export const LoadingOverlay: React.FC<LoadingOverlayProps> = ({ label = "Submitting..." }) => {
  return (
    <Stack
      styles={{
        root: {
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: 'rgba(255, 255, 255, 0.7)',
          zIndex: 1000,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center'
        }
      }}
    >
      <Spinner size={SpinnerSize.large} label={label} />
    </Stack>
  );
}; 