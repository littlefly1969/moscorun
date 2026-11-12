import { useState, type SubmitEvent } from "react";
import type { Device, DeviceKind, RegisteredDevice } from "../../api/client";
import { formatRelative } from "../../lib/format";
import { useDevices, useRegisterDevice, useRevokeDevice } from "./queries";

const kindLabel: Record<DeviceKind, string> = {
  ScaleAgent: "Agente bilancia",
  PhoneBridge: "Telefono (Health Connect)",
};

function TokenNotice({ registered, onClose }: { registered: RegisteredDevice; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  return (
    <section className="card notice" role="status" aria-labelledby="token-title">
      <h2 id="token-title">Token di {registered.device.name}</h2>
      <p>
        Copialo ora nella configurazione dell&apos;agente: per sicurezza non verrà più mostrato. Se lo perdi,
        revoca il dispositivo e creane uno nuovo.
      </p>
      <code className="token">{registered.token}</code>
      <div className="row">
        <button
          type="button"
          onClick={() => {
            void navigator.clipboard.writeText(registered.token).then(() => {
              setCopied(true);
            });
          }}
        >
          {copied ? "Copiato" : "Copia"}
        </button>
        <button type="button" className="secondary" onClick={onClose}>
          Ho salvato il token
        </button>
      </div>
    </section>
  );
}

function DeviceRow({ device }: { device: Device }) {
  const revoke = useRevokeDevice();
  const [confirming, setConfirming] = useState(false);
  const active = device.revokedAt == null;
  return (
    <tr className={active ? undefined : "rejected"}>
      <td>{device.name}</td>
      <td>{kindLabel[device.kind]}</td>
      <td>{active ? formatRelative(device.lastSeenAt) : "revocato"}</td>
      <td>
        {active &&
          (confirming ? (
            <button
              type="button"
              className="danger"
              disabled={revoke.isPending}
              onClick={() => {
                revoke.mutate(device.id);
              }}
            >
              Conferma revoca
            </button>
          ) : (
            <button
              type="button"
              className="link"
              onClick={() => {
                setConfirming(true);
              }}
            >
              Revoca
            </button>
          ))}
      </td>
    </tr>
  );
}

export function DevicesPage() {
  const devices = useDevices();
  const register = useRegisterDevice();
  const [name, setName] = useState("");
  const [kind, setKind] = useState<DeviceKind>("ScaleAgent");
  const [registered, setRegistered] = useState<RegisteredDevice | null>(null);

  const onSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault();
    register.mutate(
      { name, kind },
      {
        onSuccess: (result) => {
          setRegistered(result);
          setName("");
        },
      },
    );
  };

  return (
    <>
      <h1>Dispositivi</h1>
      {registered && (
        <TokenNotice
          registered={registered}
          onClose={() => {
            setRegistered(null);
          }}
        />
      )}

      <form className="card inline-form" onSubmit={onSubmit} aria-labelledby="new-device-title">
        <h2 id="new-device-title">Nuovo dispositivo</h2>
        <label>
          Nome
          <input
            required
            maxLength={80}
            value={name}
            onChange={(e) => {
              setName(e.target.value);
            }}
          />
        </label>
        <label>
          Tipo
          <select
            value={kind}
            onChange={(e) => {
              setKind(e.target.value as DeviceKind);
            }}
          >
            {Object.entries(kindLabel).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <button type="submit" disabled={register.isPending}>
          Crea e genera token
        </button>
        {register.isError && (
          <p role="alert" className="error">
            Registrazione non riuscita: {register.error.message}
          </p>
        )}
      </form>

      <section className="card" aria-labelledby="devices-title">
        <h2 id="devices-title">Registrati</h2>
        {devices.isPending && <p role="status">Caricamento…</p>}
        {devices.isError && <p role="alert">Impossibile caricare i dispositivi.</p>}
        {devices.data?.length === 0 && <p className="muted">Nessun dispositivo.</p>}
        {devices.data && devices.data.length > 0 && (
          <table>
            <thead>
              <tr>
                <th scope="col">Nome</th>
                <th scope="col">Tipo</th>
                <th scope="col">Ultimo contatto</th>
                <th scope="col">
                  <span className="visually-hidden">Azioni</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {devices.data.map((d) => (
                <DeviceRow key={d.id} device={d} />
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  );
}
