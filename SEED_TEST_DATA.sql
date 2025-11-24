-- ============================================
-- SQL скрипт для заполнения PostgreSQL тестовыми данными
-- Использование: psql -U yess_user -d yess_db -f SEED_TEST_DATA.sql
-- ============================================

-- Начинаем транзакцию
BEGIN;

-- ============================================
-- 1. ЗАПОЛНЕНИЕ УВЕДОМЛЕНИЙ (Notifications)
-- ============================================

-- Получаем ID существующих пользователей
DO $$
DECLARE
    user_ids INTEGER[];
    user_id INTEGER;
    notification_titles TEXT[] := ARRAY[
        'Добро пожаловать в YESS!GO',
        'Начислен кешбэк',
        'Специальное предложение',
        'Новый партнёр',
        'Обновление приложения',
        'Бонус за приглашение',
        'Достижение разблокировано',
        'Напоминание',
        'Пополнение баланса',
        'Акция выходного дня',
        'Еженедельный отчёт',
        'Новые акции',
        'Промокод активирован',
        'Партнёр рядом',
        'День рождения'
    ];
    notification_bodies TEXT[] := ARRAY[
        'Спасибо за регистрацию! Используйте приложение для получения бонусов и кешбэка у наших партнёров.',
        'Вам начислен кешбэк 50 сом за покупку в партнёре «Нават». Проверьте баланс в кошельке!',
        'Скидка 15% на все товары в партнёре «CoffeeTime» до конца недели! Не упустите возможность.',
        'К нам присоединился новый партнёр «Flask»! Получайте кешбэк 10% на все покупки.',
        'Доступна новая версия приложения с улучшенным интерфейсом и новыми функциями.',
        'Ваш друг зарегистрировался по вашей реферальной ссылке! Вам начислено 100 YessCoin.',
        'Поздравляем! Вы достигли уровня «Бронзовый партнёр». Теперь доступны дополнительные бонусы.',
        'Не забудьте использовать промокод BONUS2024 до конца месяца и получить двойной кешбэк!',
        'Ваш баланс пополнен на 500 сом. Спасибо за использование YessGo!',
        'В эти выходные кешбэк увеличен до 20% у всех партнёров категории «Рестораны»!',
        'На этой неделе вы получили 250 сом кешбэка и потратили 1500 сом. Продолжайте в том же духе!',
        'У партнёра «Sierra» стартовала акция: каждый 5-й кофе бесплатно!',
        'Промокод SUMMER2024 успешно применён. Вы получили скидку 10% на следующую покупку.',
        'Вы находитесь рядом с партнёром «Bublik»! Зайдите и получите кешбэк 8%.',
        'С днём рождения! В честь вашего праздника дарим 200 YessCoin. Используйте их для получения бонусов.'
    ];
    notification_types TEXT[] := ARRAY['info', 'info', 'promotion', 'info', 'system', 'bonus', 'achievement', 'reminder', 'payment', 'promotion', 'report', 'promotion', 'bonus', 'location', 'bonus'];
    is_read_values BOOLEAN[] := ARRAY[false, true, false, true, false, true, false, false, true, false, true, false, true, false, false];
    hours_ago INTEGER[] := ARRAY[48, 24, 12, 6, 3, 1, 0, 0, 0, 0, 72, 36, 18, 4, 0];
    i INTEGER;
    j INTEGER;
    created_at TIMESTAMP WITH TIME ZONE;
BEGIN
    -- Получаем все ID пользователей
    SELECT ARRAY_AGG(Id) INTO user_ids FROM users;
    
    -- Если нет пользователей, выходим
    IF user_ids IS NULL OR array_length(user_ids, 1) IS NULL THEN
        RAISE NOTICE 'Нет пользователей в базе данных. Создайте пользователей перед заполнением уведомлений.';
        RETURN;
    END IF;
    
    RAISE NOTICE 'Найдено пользователей: %', array_length(user_ids, 1);
    
    -- Для каждого пользователя создаём уведомления
    FOREACH user_id IN ARRAY user_ids
    LOOP
        -- Создаём приветственное уведомление (если его ещё нет)
        IF NOT EXISTS (
            SELECT 1 FROM "Notifications" 
            WHERE "UserId" = user_id 
            AND "Title" = 'Добро пожаловать в YESS!GO'
        ) THEN
            INSERT INTO "Notifications" ("UserId", "Title", "Body", "Type", "IsRead", "CreatedAt")
            VALUES (
                user_id,
                'Добро пожаловать в YESS!GO',
                'Спасибо за регистрацию в приложении YESS!GO. Желаем приятного пользования!',
                'info',
                false,
                NOW() - INTERVAL '2 days'
            );
        END IF;
        
        -- Создаём остальные тестовые уведомления
        FOR i IN 1..array_length(notification_titles, 1)
        LOOP
            -- Пропускаем первое (приветственное), оно уже создано
            IF i = 1 THEN
                CONTINUE;
            END IF;
            
            created_at := NOW() - (hours_ago[i] || ' hours')::INTERVAL;
            
            INSERT INTO "Notifications" ("UserId", "Title", "Body", "Type", "IsRead", "CreatedAt")
            VALUES (
                user_id,
                notification_titles[i],
                notification_bodies[i],
                notification_types[i],
                is_read_values[i],
                created_at
            );
        END LOOP;
    END LOOP;
    
    RAISE NOTICE 'Уведомления успешно созданы для всех пользователей';
END $$;

-- ============================================
-- 2. ЗАПОЛНЕНИЕ ТРАНЗАКЦИЙ (Transactions)
-- ============================================

DO $$
DECLARE
    user_ids INTEGER[];
    user_id INTEGER;
    partner_ids INTEGER[];
    partner_id INTEGER;
    transaction_types TEXT[] := ARRAY['topup', 'discount', 'bonus', 'refund', 'payment'];
    transaction_statuses TEXT[] := ARRAY['completed', 'completed', 'completed', 'completed', 'completed'];
    amounts DECIMAL[] := ARRAY[500.00, 150.00, 100.00, 200.00, 300.00, 250.00, 400.00, 50.00, 600.00, 75.00, 350.00, 125.00, 450.00, 80.00, 550.00];
    descriptions TEXT[] := ARRAY[
        'Пополнение баланса через банковскую карту',
        'Скидка в партнёре «Нават»',
        'Бонус за приглашение друга',
        'Возврат средств за отменённый заказ',
        'Оплата в партнёре «CoffeeTime»',
        'Пополнение баланса',
        'Бонус от партнёра «Flask»',
        'Скидка в партнёре «Sierra»',
        'Пополнение баланса через электронный кошелёк',
        'Оплата в партнёре «Bublik»',
        'Бонус за активность',
        'Скидка в партнёре «Ants»',
        'Пополнение баланса',
        'Оплата в партнёре «Supara»',
        'Бонус за день рождения'
    ];
    i INTEGER;
    j INTEGER;
    transaction_type TEXT;
    transaction_status TEXT;
    amount DECIMAL;
    description TEXT;
    created_at TIMESTAMP WITH TIME ZONE;
    balance_before DECIMAL;
    balance_after DECIMAL;
    current_balance DECIMAL := 0;
BEGIN
    -- Получаем все ID пользователей
    SELECT ARRAY_AGG(Id) INTO user_ids FROM users;
    
    -- Получаем все ID партнёров (если есть)
    SELECT ARRAY_AGG(Id) INTO partner_ids FROM partners WHERE Id IS NOT NULL;
    
    -- Если нет пользователей, выходим
    IF user_ids IS NULL OR array_length(user_ids, 1) IS NULL THEN
        RAISE NOTICE 'Нет пользователей в базе данных. Создайте пользователей перед заполнением транзакций.';
        RETURN;
    END IF;
    
    RAISE NOTICE 'Найдено пользователей: %', array_length(user_ids, 1);
    IF partner_ids IS NOT NULL THEN
        RAISE NOTICE 'Найдено партнёров: %', array_length(partner_ids, 1);
    END IF;
    
    -- Для каждого пользователя создаём транзакции
    FOREACH user_id IN ARRAY user_ids
    LOOP
        current_balance := 0;
        
        -- Получаем текущий баланс из кошелька (если есть)
        SELECT COALESCE("Balance", 0) INTO current_balance 
        FROM wallets 
        WHERE "UserId" = user_id 
        LIMIT 1;
        
        -- Создаём 15-20 транзакций для каждого пользователя
        FOR i IN 1..15
        LOOP
            -- Выбираем случайный тип транзакции
            transaction_type := transaction_types[1 + (i % array_length(transaction_types, 1))];
            transaction_status := transaction_statuses[1 + (i % array_length(transaction_statuses, 1))];
            amount := amounts[1 + (i % array_length(amounts, 1))];
            description := descriptions[1 + (i % array_length(descriptions, 1))];
            
            -- Выбираем случайного партнёра для транзакций типа payment, discount
            partner_id := NULL;
            IF transaction_type IN ('payment', 'discount') AND partner_ids IS NOT NULL THEN
                partner_id := partner_ids[1 + ((i * user_id) % array_length(partner_ids, 1))];
            END IF;
            
            -- Вычисляем баланс до и после транзакции
            balance_before := current_balance;
            
            IF transaction_type IN ('topup', 'bonus', 'refund') THEN
                balance_after := current_balance + amount;
                current_balance := balance_after;
            ELSIF transaction_type IN ('payment', 'discount') THEN
                balance_after := current_balance - amount;
                current_balance := balance_after;
            ELSE
                balance_after := current_balance;
            END IF;
            
            -- Создаём дату транзакции (от 30 дней назад до сейчас)
            created_at := NOW() - (RANDOM() * 30 || ' days')::INTERVAL;
            
            -- Вставляем транзакцию
            INSERT INTO transactions (
                "UserId", 
                "PartnerId", 
                "Type", 
                "Amount", 
                "Status", 
                "Description",
                "BalanceBefore",
                "BalanceAfter",
                "CreatedAt",
                "CompletedAt"
            )
            VALUES (
                user_id,
                partner_id,
                transaction_type,
                amount,
                transaction_status,
                description,
                balance_before,
                balance_after,
                created_at,
                CASE WHEN transaction_status = 'completed' THEN created_at + INTERVAL '1 minute' ELSE NULL END
            );
        END LOOP;
        
        -- Обновляем баланс в кошельке
        UPDATE wallets 
        SET "Balance" = current_balance, "LastUpdated" = NOW()
        WHERE "UserId" = user_id;
        
        RAISE NOTICE 'Создано 15 транзакций для пользователя ID: %', user_id;
    END LOOP;
    
    RAISE NOTICE 'Транзакции успешно созданы для всех пользователей';
END $$;

-- ============================================
-- 3. ПРОВЕРКА РЕЗУЛЬТАТОВ
-- ============================================

DO $$
DECLARE
    notification_count INTEGER;
    transaction_count INTEGER;
    user_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO user_count FROM users;
    SELECT COUNT(*) INTO notification_count FROM "Notifications";
    SELECT COUNT(*) INTO transaction_count FROM transactions;
    
    RAISE NOTICE '========================================';
    RAISE NOTICE 'РЕЗУЛЬТАТЫ ЗАПОЛНЕНИЯ БД:';
    RAISE NOTICE 'Пользователей: %', user_count;
    RAISE NOTICE 'Уведомлений: %', notification_count;
    RAISE NOTICE 'Транзакций: %', transaction_count;
    RAISE NOTICE '========================================';
END $$;

-- Коммитим транзакцию
COMMIT;

-- ============================================
-- СКРИПТ ЗАВЕРШЁН
-- ============================================

