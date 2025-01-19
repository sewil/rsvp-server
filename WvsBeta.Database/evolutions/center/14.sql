------- UP -------

ALTER TABLE character_rate_credits ADD COLUMN enabled TINYINT(1) DEFAULT 0;

------- DOWN -------

ALTER TABLE character_rate_credits DROP COLUMN enabled;