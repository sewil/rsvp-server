-- Change the creditrate to 10x the amount (because higher accuracy)
------- UP -------

UPDATE character_rate_credits SET credits_left = credits_left * 10, credits_given = credits_given * 10;

------- DOWN -------

UPDATE character_rate_credits SET credits_left = credits_left / 10, credits_given = credits_given / 10;
