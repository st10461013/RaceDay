/* Run once against RaceDayDB after the Part 1/2 schema has been created. */
IF COL_LENGTH('dbo.Events', 'BannerImageUrl') IS NULL
    ALTER TABLE dbo.Events ADD BannerImageUrl VARCHAR(1000) NULL;
IF COL_LENGTH('dbo.Users', 'ProfileImageUrl') IS NULL
    ALTER TABLE dbo.Users ADD ProfileImageUrl VARCHAR(1000) NULL;
