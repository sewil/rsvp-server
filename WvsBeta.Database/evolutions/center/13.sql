------- UP -------

UPDATE character_quests SET questid = 1005700 WHERE questid = 1005600;
UPDATE character_quests SET questid = 1005701 WHERE questid = 1005802;
UPDATE character_quests SET questid = 1005702 WHERE questid = 1005803;

UPDATE character_quests SET questid = 1005600 WHERE questid = 1005800;
UPDATE character_quests SET questid = 1005601 WHERE questid = 1005801;

UPDATE character_quests SET questid = 300, data = 'end' WHERE questid = 2000700 AND data = '14';
DELETE FROM character_quests WHERE questid = 2000700;
DELETE FROM character_quests WHERE questid = 5;

------- DOWN -------


UPDATE character_quests SET questid = 1005801 WHERE questid = 1005601;
UPDATE character_quests SET questid = 1005800 WHERE questid = 1005600;

UPDATE character_quests SET questid = 1005803 WHERE questid = 1005702;
UPDATE character_quests SET questid = 1005802 WHERE questid = 1005701;
UPDATE character_quests SET questid = 1005600 WHERE questid = 1005700;

UPDATE character_quests SET questid = 2000700, data = '14' WHERE questid = 300 AND data = 'end';
