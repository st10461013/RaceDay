## Routes
- RouteID (PK)
- EventID (FK - Events.Event.ID)
- RouteName
- DistanceKM
- ElevationGainM
- MapURL

## Categories
- CategoryId (PK)
- EventID (FK - Events.EventID)
- CategoryName
- DistanceKM
- MaxPArticipants
- EntryFee

Relationships: Events 1 --- M Routes, Events 1 --- M Categories
