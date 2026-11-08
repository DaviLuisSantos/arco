import { currentLocale } from '../i18n'

/** Rótulo no idioma atual (lido a cada acesso, então troca junto com o idioma). */
function zone(id: string, pt: string, en: string = pt): { id: string; readonly label: string } {
  return { id, get label() { return currentLocale() === 'en' ? en : pt } }
}

export const COMMON_TIMEZONES: Array<{ id: string; readonly label: string }> = [
  zone('America/Sao_Paulo', 'Brasília (São Paulo)'),
  zone('America/Manaus', 'Manaus'),
  zone('America/Cuiaba', 'Cuiabá'),
  zone('America/Belem', 'Belém'),
  zone('America/Fortaleza', 'Fortaleza'),
  zone('America/Recife', 'Recife'),
  zone('America/Bahia', 'Salvador'),
  zone('America/Rio_Branco', 'Rio Branco'),
  zone('America/Noronha', 'Fernando de Noronha'),
  zone('America/Argentina/Buenos_Aires', 'Buenos Aires'),
  zone('America/Santiago', 'Santiago'),
  zone('America/Bogota', 'Bogotá'),
  zone('America/Mexico_City', 'Cidade do México', 'Mexico City'),
  zone('America/New_York', 'Nova York', 'New York'),
  zone('America/Chicago', 'Chicago'),
  zone('America/Denver', 'Denver'),
  zone('America/Los_Angeles', 'Los Angeles'),
  zone('Europe/Lisbon', 'Lisboa', 'Lisbon'),
  zone('Europe/London', 'Londres', 'London'),
  zone('Europe/Madrid', 'Madri', 'Madrid'),
  zone('Europe/Paris', 'Paris'),
  zone('Europe/Berlin', 'Berlim', 'Berlin'),
  zone('Africa/Luanda', 'Luanda'),
  zone('Africa/Maputo', 'Maputo'),
  zone('Asia/Dubai', 'Dubai'),
  zone('Asia/Tokyo', 'Tóquio', 'Tokyo'),
  zone('Australia/Sydney', 'Sydney'),
  zone('UTC', 'UTC'),
]
