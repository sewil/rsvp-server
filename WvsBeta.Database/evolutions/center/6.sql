------- UP -------
ALTER TABLE cashshop_coupon_codes              ENGINE = InnoDB;
ALTER TABLE cashshop_coupon_item_rewards       ENGINE = InnoDB;
ALTER TABLE cashshop_limit_sell                ENGINE = InnoDB;
ALTER TABLE cashshop_modified_items            ENGINE = InnoDB;
ALTER TABLE cashshop_sell_log                  ENGINE = InnoDB;
ALTER TABLE character_cashshop_gifts           ENGINE = InnoDB;
ALTER TABLE character_quest_mobs               ENGINE = InnoDB;
ALTER TABLE character_quests                   ENGINE = InnoDB;
ALTER TABLE character_variables                ENGINE = InnoDB;
ALTER TABLE character_wishlist                 ENGINE = InnoDB;
ALTER TABLE completed_quests                   ENGINE = InnoDB;
ALTER TABLE connections                        ENGINE = InnoDB;
ALTER TABLE cooldowns                          ENGINE = InnoDB;
ALTER TABLE fame_log                           ENGINE = InnoDB;
ALTER TABLE inc_table                          ENGINE = InnoDB;
ALTER TABLE pets                               ENGINE = InnoDB;
ALTER TABLE skills                             ENGINE = InnoDB;
ALTER TABLE teleport_rock_locations            ENGINE = InnoDB;
ALTER TABLE storage                            ENGINE = InnoDB;
ALTER TABLE storage_cash                       ENGINE = InnoDB;
ALTER TABLE storage_cashshop                   ENGINE = InnoDB;
ALTER TABLE storage_items                      ENGINE = InnoDB;


------- DOWN -------

ALTER TABLE cashshop_coupon_codes              ENGINE = MyISAM;
ALTER TABLE cashshop_coupon_item_rewards       ENGINE = MyISAM;
ALTER TABLE cashshop_limit_sell                ENGINE = MyISAM;
ALTER TABLE cashshop_modified_items            ENGINE = MyISAM;
ALTER TABLE cashshop_sell_log                  ENGINE = MyISAM;
ALTER TABLE character_cashshop_gifts           ENGINE = MyISAM;
ALTER TABLE character_quest_mobs               ENGINE = MyISAM;
ALTER TABLE character_quests                   ENGINE = MyISAM;
ALTER TABLE character_variables                ENGINE = MyISAM;
ALTER TABLE character_wishlist                 ENGINE = MyISAM;
ALTER TABLE completed_quests                   ENGINE = MyISAM;
ALTER TABLE connections                        ENGINE = MyISAM;
ALTER TABLE cooldowns                          ENGINE = MyISAM;
ALTER TABLE fame_log                           ENGINE = MyISAM;
ALTER TABLE inc_table                          ENGINE = MyISAM;
ALTER TABLE pets                               ENGINE = MyISAM;
ALTER TABLE skills                             ENGINE = MyISAM;
ALTER TABLE teleport_rock_locations            ENGINE = MyISAM;
ALTER TABLE storage                            ENGINE = MyISAM;
ALTER TABLE storage_cash                       ENGINE = MyISAM;
ALTER TABLE storage_cashshop                   ENGINE = MyISAM;
ALTER TABLE storage_items                      ENGINE = MyISAM;