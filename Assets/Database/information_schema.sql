-- 1. 테이블 구조 만들기
CREATE TABLE information_items (
    info_id VARCHAR(20) PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    grade VARCHAR(20) NOT NULL,
    price BIGINT NOT NULL,
    accuracy DECIMAL(4,3) NOT NULL,
    target_asset VARCHAR(20) NOT NULL,
    prediction_type VARCHAR(30) NOT NULL,
    lookahead_turns INT NOT NULL DEFAULT 1,
    free_with_item VARCHAR(20),
    description VARCHAR(255)
);

-- 2. 실제 데이터 넣기
INSERT INTO information_items
(
    info_id,
    name,
    grade,
    price,
    accuracy,
    target_asset,
    prediction_type,
    lookahead_turns,
    free_with_item,
    description
)
VALUES
(
    'INFO_LOW',
    '하급 정보',
    'LOW',
    100000,
    0.60,
    'TECH',
    'DIRECTION',
    1,
    'SHOP_006',
    '다음 턴 TECH 시장의 상승 또는 하락 방향을 60% 정확도로 제공한다.'
),
(
    'INFO_MID',
    '중급 정보',
    'MID',
    500000,
    0.75,
    'TECH',
    'DIRECTION',
    1,
    NULL,
    '다음 턴 TECH 시장의 상승 또는 하락 방향을 75% 정확도로 제공한다.'
),
(
    'INFO_HIGH',
    '고급 정보',
    'HIGH',
    2000000,
    0.90,
    'TECH',
    'DIRECTION',
    1,
    NULL,
    '다음 턴 TECH 시장의 상승 또는 하락 방향을 90% 정확도로 제공한다.'
);