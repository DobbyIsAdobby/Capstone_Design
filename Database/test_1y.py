import yfinance as yf
import pandas as pd
from pathlib import Path


BASE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = BASE_DIR.parent

OUTPUT_DIR = PROJECT_DIR / "Assets" / "Resources" / "Data"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

OUTPUT_FILE = OUTPUT_DIR / "market_data_test.csv"


# 1. 나스닥 데이터 수집
nasdaq = yf.Ticker("^IXIC").history(
    period="2y",
    interval="1d",
    auto_adjust=True
)

if nasdaq.empty:
    raise RuntimeError("NASDAQ 데이터를 가져오지 못했습니다.")


# 2. 종가 데이터 추출
close = nasdaq["Close"]


# 3. 월말 종가로 변환
monthly = close.resample("ME").last().dropna()


# 4. 현재 진행 중인 달 제외
now = pd.Timestamp.now()
last_date = monthly.index[-1]

if last_date.year == now.year and last_date.month == now.month:
    monthly = monthly.iloc[:-1]


# 5. 최근 13개월 데이터 선택
monthly = monthly.tail(13)

if len(monthly) < 13:
    raise RuntimeError("12개월 수익률을 계산하기 위한 데이터가 부족합니다.")


# 6. 월별 수익률 계산
returns = monthly.pct_change().dropna()


# 7. 12턴 테스트 데이터 생성
result = pd.DataFrame({
    "turn": range(1, 13),
    "date": returns.index.strftime("%Y-%m"),
    "return_rate": returns.values
})

result["return_rate"] = result["return_rate"].round(6)


# 8. CSV 저장
result.to_csv(
    OUTPUT_FILE,
    index=False
)


print("\nNASDAQ 1년 테스트 데이터 생성 완료")
print(result)

print(f"\n저장 위치: {OUTPUT_FILE}")