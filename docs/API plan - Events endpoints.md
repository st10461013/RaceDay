## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events | Browse/search all upcoming events (supports query filters e.g. `?location=` `?date=`) | Public | — | `200 OK` — array of events |
| GET | /api/events/{eventId} | Get full details for a single event | Public | — | `200 OK` — event object |
| POST | /api/events | Create a new event | Organiser | `{ eventName, eventDate, location, description, weatherLocationCode }` | `201 Created` — new event object |
| PUT | /api/events/{eventId} | Update an event owned by the logged-in organiser | Organiser | `{ eventName, eventDate, location, description, weatherLocationCode }` | `200 OK` — updated event object |
| DELETE | /api/events/{eventId} | Delete an event owned by the logged-in organiser | Organiser | — | `204 No Content` |
