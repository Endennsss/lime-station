# Вспомогательные формы русского языка для Fluent-функций движка.
zzzz-the = { $ent }

zzzz-subject-pronoun = { GENDER($ent) ->
    [male] он
    [female] она
    [epicene] они
   *[neuter] оно
   }

zzzz-object-pronoun = { GENDER($ent) ->
    [male] его
    [female] её
    [epicene] их
   *[neuter] его
   }

zzzz-dat-object = { GENDER($ent) ->
    [male] ему
    [female] ей
    [epicene] им
   *[neuter] ему
   }

zzzz-genitive = { GENDER($ent) ->
    [male] его
    [female] её
    [epicene] их
   *[neuter] его
   }

zzzz-possessive-pronoun = { GENDER($ent) ->
    [male] его
    [female] её
    [epicene] их
   *[neuter] его
   }

zzzz-possessive-adjective = { GENDER($ent) ->
    [male] его
    [female] её
    [epicene] их
   *[neuter] его
   }

zzzz-reflexive-pronoun = { GENDER($ent) ->
    [male] себя
    [female] себя
    [epicene] себя
   *[neuter] себя
   }

zzzz-conjugate-be = есть
zzzz-conjugate-have = имеет
zzzz-conjugate-basic = { $second }
