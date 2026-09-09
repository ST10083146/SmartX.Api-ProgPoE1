import React, { useState } from 'react';
import { api } from '../api/client.js';

export default function FileUploader({ sensor, onUploaded }) {
  const [file, setFile] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const submit = async () => {
    if (!file || !sensor) return;
    setBusy(true);
    setError(null);
    try {
      const result = await api.uploadFile(sensor.id, file);
      onUploaded(result.path);
      setFile(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  };

  if (!sensor) {
    return <p className="hint-text">Select a registered sensor to attach a config file, deployment photo, or log.</p>;
  }

  return (
    <div>
      <p className="hint-text">Attaching to: <strong>{sensor.nodeId}</strong></p>
      <div className="upload-row">
        <input type="file" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
        <button type="button" onClick={submit} disabled={!file || busy} style={{ width: 'auto', marginTop: 0 }}>
          {busy ? 'Uploading…' : 'Attach'}
        </button>
      </div>
      {error && <p className="error-text">{error}</p>}
      {sensor.attachedFiles?.length > 0 && (
        <div style={{ marginTop: '0.75rem' }}>
          {sensor.attachedFiles.map((f) => (
            <span key={f} className="file-chip">{f.split('/').pop()}</span>
          ))}
        </div>
      )}
    </div>
  );
}
