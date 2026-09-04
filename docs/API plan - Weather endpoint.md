## Weather

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/weather | Get the live weather forecast for an event's location, to help participants prepare for race day | Organiser or Participant | — | `200 OK` — `{ location, forecastDate, temperature, conditions, windSpeed }` |
