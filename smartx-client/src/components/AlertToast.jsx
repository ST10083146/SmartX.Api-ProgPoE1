import React from 'react';
import { useTelemetry } from '../context/TelemetryContext.jsx';

// Lives in App so alerts show up on every page, not just the ingestion page.
export default function AlertToast() {
  const { toast } = useTelemetry();
  if (!toast) return null;
  return <div className="alert-toast">⚠ {toast.message}</div>;
}
